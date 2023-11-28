﻿using GH_IO.Serialization;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using SimpleGrasshopper.Attributes;
using SimpleGrasshopper.Util;
using System.Collections;

namespace SimpleGrasshopper.DocumentObjects;

/// <summary>
/// The <see cref="GH_Component"/> that targets to a <see cref="System.Reflection.MethodInfo"/>.
/// </summary>
public abstract class MethodComponent(
        MethodInfo[] methodInfos,
        string? name = null,
        string? nickName = null,
        string? description = null,
        string? subCategory = null,
        string? iconPath = null,
        GH_Exposure? exposure = null)
    : GH_TaskCapableComponent<(object?, object?[])>(
        name ?? methodInfos[0].GetDocObjName(),
        nickName ?? methodInfos[0].GetDocObjNickName(),
        description ?? methodInfos[0].GetDocObjDescription(),
        string.Empty,
        subCategory ?? methodInfos[0].GetDeclaringClassName())
    , IGH_VariableParameterComponent
{
    private readonly record struct OutputData(string Name, int Index, GH_ParamAccess Access);

    private int _methodIndex = 0;
    private int MethodIndex
    {
        get => _methodIndex;
        set
        {
            if (value == _methodIndex) return;
            var count = methodInfos.Length;
            value = (value + count) % count;

            if (value == _methodIndex) return;
            _methodIndex = value;

            if (name == null)
            {
                Name = MethodInfo.GetDocObjName();
            }
            if (nickName == null)
            {
                NickName = MethodInfo.GetDocObjNickName();
            }
            if (description == null)
            {
                Description = MethodInfo.GetDocObjDescription();
            }

            //Destroy
            Params.Clear();
            DestroyIconCache();

            //Build
            _changing = true;
            PostConstructor();
            _changing = false;

            //Update
            ExpireSolution(true);
            Attributes.ExpireLayout();
            Instances.ActiveCanvas.Refresh();
        }
    }

    /// <summary>
    /// The declaring type of this method.
    /// </summary>
    protected virtual Type? DeclaringType { get; } = null;

    private MethodInfo MethodInfo => methodInfos[MethodIndex];

    /// <inheritdoc/>
    public override string Category 
    { 
        get => GetType().GetAssemblyName();
        set => base.Category = value; 
    }

    /// <inheritdoc/>
    public override GH_Exposure Exposure
    {
        get
        {
            if(exposure.HasValue) return exposure.Value;

            foreach (var method in methodInfos)
            {
                var ex = method.GetCustomAttribute<ExposureAttribute>()?.Exposure;
                if (ex.HasValue) return ex.Value;
            }
            return base.Exposure;
        }
    }

    private readonly Dictionary<int, Bitmap> _icons = [];

    /// <inheritdoc/>
    protected override Bitmap Icon
    {
        get
        {
            if (_icons.TryGetValue(MethodIndex, out Bitmap? icon)) return icon;

            var path = MethodInfo.GetCustomAttribute<IconAttribute>()?.IconPath ?? iconPath;
            if (path == null) return base.Icon;

            return _icons[MethodIndex] = GetType().Assembly.GetBitmap(path) ?? base.Icon;
        }
    }

    private static IGH_Param? GetParamFromType(Type type, out GH_ParamAccess access)
    {
        type = type.GetRawType().GetAccessAndType(out access);

        var proxy = Instances.ComponentServer.EmitObjectProxy(type.GetDocObjGuid());

        if (proxy.CreateInstance() is not IGH_Param param) return null;

        if (type.IsEnum && param is Param_Integer integerParam)
        {
            var names = Enum.GetNames(type);
            int index = 0;
            var underType = Enum.GetUnderlyingType(type);
            foreach (object obj in Enum.GetValues(type))
            {
                var v = Convert.ToInt32(Convert.ChangeType(obj, underType));
                integerParam.AddNamedValue(names[index++], v);
            }
        }
        return param;
    }

    /// <inheritdoc/>
    protected sealed override void RegisterInputParams(GH_InputParamManager pManager)
    {
        if (CanCreateDeclaringType(out var type, out var access, out var gh_paramMajor))
        {
            var attr = type.GetCustomAttribute<DocObjAttribute>();
            var defaultName = type.Name ?? string.Empty;

            pManager.AddParameter(gh_paramMajor, attr?.Name ?? defaultName, attr?.NickName ?? defaultName, attr?.Description ?? defaultName, access);
        }

        foreach (var param in MethodInfo.GetParameters().Where(IsIn))
        {
            if (GetParameter(param, out access)
                is not IGH_Param gh_param) continue;

            var attr = param.GetCustomAttribute<DocObjAttribute>();
            var defaultName = param.Name ?? "Input";

            pManager.AddParameter(gh_param, attr?.Name ?? defaultName, attr?.NickName ?? defaultName, attr?.Description ?? defaultName, access);
        }
    }

    /// <inheritdoc/>
    protected sealed override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        if (CanCreateDeclaringType(out var type, out var access, out var gh_paramMajor))
        {
            var attr = type.GetCustomAttribute<DocObjAttribute>();
            var defaultName = type.Name ?? string.Empty;

            pManager.AddParameter(gh_paramMajor, attr?.Name ?? defaultName, attr?.NickName ?? defaultName, attr?.Description ?? defaultName, access);
        }

        if (MethodInfo.ReturnParameter is ParameterInfo paramInfo
            && paramInfo.ParameterType != typeof(void)
            && GetParameter(paramInfo, out access) is IGH_Param gh_returnParam)
        {
            var attr = paramInfo.GetCustomAttribute<DocObjAttribute>();
            var defaultName = paramInfo.Name ?? "Result";

            pManager.AddParameter(gh_returnParam, attr?.Name ?? defaultName, attr?.NickName ?? defaultName, attr?.Description ?? defaultName, access);
        }

        foreach (var param in MethodInfo.GetParameters().Where(IsOut))
        {
            if (GetParameter(param, out access)
                is not IGH_Param gh_param) continue;

            var attr = param.GetCustomAttribute<DocObjAttribute>();
            var defaultName = param.Name ?? "Output";

            pManager.AddParameter(gh_param, attr?.Name ?? defaultName, attr?.NickName ?? defaultName, attr?.Description ?? defaultName, access);
        }
    }

    private bool CanCreateDeclaringType(out Type type, out GH_ParamAccess access, out IGH_Param param)
    {
        type = null!;
        param = null!;
        access = GH_ParamAccess.item;
        if (MethodInfo.IsStatic) return false;
        if (MethodInfo.DeclaringType is not Type t) return false;
        type = DeclaringType ?? t;
        if (GetParamFromType(type, out access) is not IGH_Param gh_paramMajor) return false;
        param = gh_paramMajor;
        return true;
    }

    private static IGH_Param? GetParameter(ParameterInfo info, out GH_ParamAccess access)
    {
        access = GH_ParamAccess.item;

        var type = info.ParameterType.GetRawType();
        if (type == null) return null;

        type = type.GetAccessAndType(out access);

        var proxy = Instances.ComponentServer.EmitObjectProxy(
            info.GetCustomAttribute<ParamAttribute>()?.Guid ?? type.GetDocObjGuid());

        if (proxy.CreateInstance() is not IGH_Param param) return null;

        SetOptional(info, param, access);

        if (type.IsEnum && param is Param_Integer integerParam)
        {
            var names = Enum.GetNames(type);
            int index = 0;
            var underType = Enum.GetUnderlyingType(type);
            foreach (object obj in Enum.GetValues(type))
            {
                var v = Convert.ToInt32(Convert.ChangeType(obj, underType));
                integerParam.AddNamedValue(names[index++], v);
            }
        }
        else if (param is Param_Number numberParam && info.GetCustomAttribute<AngleAttribute>() != null)
        {
            numberParam.AngleParameter = true;
        }
        else if (param is IGH_PreviewObject previewObject && info.GetCustomAttribute<HiddenAttribute>() != null)
        {
            previewObject.Hidden = true;
        }

        return param;

        static void SetOptional(ParameterInfo info, IGH_Param param, GH_ParamAccess access)
        {
            if (access == GH_ParamAccess.item && info.DefaultValue != null)
            {
                SetPersistentData(ref param, info.DefaultValue);
            }
            else if (info.HasDefaultValue)
            {
                param.Optional = true;
            }

            static void SetPersistentData(ref IGH_Param param, object data)
            {
                var persistType = typeof(GH_PersistentParam<>);
                if (param.GetType().IsGeneralType(persistType) is not Type persistParam) return;

                var method = persistType.MakeGenericType(persistParam).GetRuntimeMethod("SetPersistentData", [typeof(object[])]);

                if (method == null) return;
                method.Invoke(param, [new object[] { data }]);
            }
        }
    }

    private static bool IsIn(ParameterInfo info)
    {
        return !(info.ParameterType.IsByRef && info.IsOut);
    }

    private static bool IsOut(ParameterInfo info)
    {
        return info.ParameterType.IsByRef;
    }

    /// <inheritdoc/>
    protected sealed override void SolveInstance(IGH_DataAccess DA)
    {
        var ps = MethodInfo.GetParameters();
        if (ps == null) return;

        var outParams = new List<OutputData>(ps.Length);
        object? obj = null;

        var isNotStatic = false;
        GH_ParamAccess classAccess = GH_ParamAccess.item;
        if (!MethodInfo.IsStatic && MethodInfo.DeclaringType is Type classType)
        {
            isNotStatic = true;
            classType = classType.GetRawType().GetAccessAndType(out classAccess);
            DA.GetValue(0, classType, classAccess, out obj);
        }

        int index = -1;
        var parameters = ps.Select(param =>
        {
            index++;

            var type = param.ParameterType.GetRawType();
            if (type == null) return null;

            type = type.GetAccessAndType(out var access);

            var name = param.GetCustomAttribute<DocObjAttribute>()?.Name
                ?? param.Name ?? string.Empty;

            if (IsOut(param))
            {
                outParams.Add(new(name, index, access));
            }
            
            if (IsIn(param))
            {
                DA.GetValue(name, type, access, out var obj);
                return obj;
            }

            return access switch
            {
                GH_ParamAccess.list => new List<object>(),
                GH_ParamAccess.tree => new GH_Structure<IGH_Goo>(),
                _ => null,
            };
        }).ToArray();

        if (InPreSolve)
        {
            TaskList.Add(Task.Run(() =>
            {
                var result = MethodInfo.Invoke(obj, parameters);
                return (result, parameters);
            }));
            return;
        }

        if (!GetSolveResults(DA, out var resultParams))
        {
            var result = MethodInfo.Invoke(obj, parameters);
            resultParams = (result, parameters);
        }

        #region Set Data
        if (isNotStatic)
        {
            switch (classAccess)
            {
                case GH_ParamAccess.item:
                    DA.SetData(0, obj);
                    break;

                case GH_ParamAccess.list:
                    DA.SetDataList(0, obj as IEnumerable);
                    break;

                case GH_ParamAccess.tree:
                    DA.SetDataTree(0, obj as IGH_Structure);
                    break;
            }
        }

        if (MethodInfo.ReturnType != typeof(void))
        {
            var returnIndex = isNotStatic ? 1 : 0;
            switch (Params.Output[returnIndex].Access)
            {
                case GH_ParamAccess.item:
                    DA.SetData(returnIndex, resultParams.Item1);
                    break;

                case GH_ParamAccess.list:
                    DA.SetDataList(returnIndex, resultParams.Item1 as IEnumerable);
                    break;

                case GH_ParamAccess.tree:
                    DA.SetDataTree(returnIndex, resultParams.Item1 as IGH_Structure);
                    break;
            }
        }

        foreach (var param in outParams)
        {
            var result = resultParams.Item2[param.Index];
            switch (param.Access)
            {
                case GH_ParamAccess.tree:
                    DA.SetDataTree(Params.IndexOfOutputParam(param.Name), result as IGH_Structure);
                    break;

                case GH_ParamAccess.list:
                    DA.SetDataList(param.Name, result as IEnumerable);
                    break;

                default:
                    DA.SetData(param.Name, result);
                    break;
            }
        }
        #endregion
    }

    /// <inheritdoc/>
    public override bool Read(GH_IReader reader)
    {
        return reader.TryGetInt32(nameof(_methodIndex), ref _methodIndex)
            && base.Read(reader);
    }

    /// <inheritdoc/>
    public override bool Write(GH_IWriter writer)
    {
        writer.SetInt32(nameof(_methodIndex), _methodIndex);
        return base.Write(writer);
    }

    /// <inheritdoc/>
    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);

        var count = methodInfos.Length;
        if (count < 2) return;

        for (int i = 0; i < count; i++)
        {
            var method = methodInfos[i];

            var item = new ToolStripMenuItem
            {
                Text = $"{method.GetDocObjName()} ({method.GetDocObjNickName()})",
                ToolTipText = method.GetDocObjDescription(),
                Checked = i == MethodIndex,
                Tag = i,
            };

            item.Click += (sender, e) =>
            {
                MethodIndex = (int)((ToolStripMenuItem)sender!).Tag;
            };

            menu.Items.Add(item);
        }
    }

    private bool _changing = false;

    /// <inheritdoc/>
    public sealed override void CreateAttributes()
    {
        if (!_changing || m_attributes == null)
        {
            m_attributes = CreateAttribute();
        }
    }

    /// <summary>
    /// Your custom <see cref="IGH_Attributes"/>
    /// </summary>
    /// <returns>the attribute you want.</returns>
    public virtual IGH_Attributes CreateAttribute()
    {
        return new GH_ComponentAttributes(this);
    }

    /// <inheritdoc/>
    public virtual bool CanInsertParameter(GH_ParameterSide side, int index) => false;

    /// <inheritdoc/>
    public virtual bool CanRemoveParameter(GH_ParameterSide side, int index) => false;

    /// <inheritdoc/>
    public virtual IGH_Param CreateParameter(GH_ParameterSide side, int index) => null!;

    /// <inheritdoc/>
    public virtual bool DestroyParameter(GH_ParameterSide side, int index) => false;

    /// <inheritdoc/>
    public virtual void VariableParameterMaintenance()
    {
    }
}
