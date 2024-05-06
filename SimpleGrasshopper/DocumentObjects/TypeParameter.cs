﻿using GH_IO.Serialization;
using SimpleGrasshopper.Attributes;
using SimpleGrasshopper.Data;
using SimpleGrasshopper.Util;
using System;

namespace SimpleGrasshopper.DocumentObjects;

/// <summary>
/// A simple <see cref="GH_PersistentParam{T}"/> for one object.
/// </summary>
/// <typeparam name="T">the object that it contains.</typeparam>
public abstract class TypeParameter<T>()
    : GH_PersistentParam<SimpleGoo<T>>(typeof(T).GetDocObjName(),
                   typeof(T).GetDocObjNickName(),
                   typeof(T).GetDocObjDescription(),
                   typeof(T).GetAssemblyName(),
                   "Parameters")
{
    /// <inheritdoc/>
    public override GH_Exposure Exposure => typeof(T).GetCustomAttribute<ExposureAttribute>()?.Exposure ?? base.Exposure;

    private Bitmap? _icon;
    /// <inheritdoc/>
    protected override Bitmap Icon
    {
        get
        {
            if (_icon != null) return _icon;
            var path = typeof(T).GetCustomAttribute<IconAttribute>()?.IconPath;
            if (path == null) return base.Icon;

            return _icon = GetType().Assembly.GetBitmap(path) ?? base.Icon;
        }
    }

    /// <inheritdoc/>
    protected override GH_GetterResult Prompt_Plural(ref List<SimpleGoo<T>> values)
    {
        try
        {
            values ??= [];
            values.Add(new SimpleGoo<T>((T)typeof(T).CreateInstance()));
            return GH_GetterResult.success;
        }
        catch
        {
            return GH_GetterResult.cancel;
        }
    }

    /// <inheritdoc/>
    protected override GH_GetterResult Prompt_Singular(ref SimpleGoo<T> value)
    {
        try
        {
            value = new SimpleGoo<T>((T)typeof(T).CreateInstance());
            return GH_GetterResult.success;
        }
        catch
        {
            return GH_GetterResult.cancel;
        }
    }

    /// <inheritdoc/>
    public override bool Read(GH_IReader reader)
    {
        reader.Read(this);
        return base.Read(reader);
    }

    /// <inheritdoc/>
    public override bool Write(GH_IWriter writer)
    {
        writer.Write(this);
        return base.Write(writer);
    }

    /// <inheritdoc/>
    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        var type = typeof(T);
        Dictionary<Type, Guid> dict = [];

        foreach (var pair in AssemblyPriority.PropertyComponentsGuid)
        {
            if (!type.IsAssignableFrom(pair.Key)) continue;

            dict[pair.Key] = pair.Value;
        }

        if (dict.Count == 0)
        {
            base.AppendAdditionalMenuItems(menu);
            return;
        }

        Guid? id = null;
        if (dict.TryGetValue(GetBestType(), out var guid))
        {
            id = guid;
        }

        switch (Kind)
        {
            case GH_ParamKind.floating:
                menu.Items.Add(GetCtor(id, dict));
                menu.Items.Add(GetDtor(id, dict));
                break;

            case GH_ParamKind.input:
                menu.Items.Add(GetCtor(id, dict));
                break;

            case GH_ParamKind.output:
                menu.Items.Add(GetDtor(id, dict));
                break;
        }

        Menu_AppendSeparator(menu);
        base.AppendAdditionalMenuItems(menu);
        return;
    }

    private Type GetBestType()
    {
        var dataTypes = this.VolatileData.AllData(true)
            .Select(goo => goo.GetType().GetRuntimeProperty("Value")?.GetValue(goo)?.GetType())
            .OfType<Type>().ToArray();

        var majorType = typeof(T);

        if (dataTypes.Length == 0)
        {
            return majorType;
        }

        majorType = dataTypes.First();

        foreach (var type in dataTypes)
        {
            majorType = Parent(majorType, type);
        }

        return majorType;
    }

    //TODO : Interfaces.
    private Type Parent(Type type1, Type type2)
    {
        if (type1.IsAssignableFrom(type2))
        {
            return type1;
        }
        else if (type2.IsAssignableFrom(type1))
        {
            return type2;
        }
        else
        {
            var result = type1;
            while (!result.IsAssignableFrom(type2))
            {
                var baseType = result.BaseType;
                if (baseType == null) break;
                result = baseType;
            }
            return result;
        }
    }

    private ToolStripMenuItem GetDtor(Guid? guid, Dictionary<Type, Guid> dict)
    {
        var result = GetDeconstructor(guid);

        SimpleUtils.SearchDropdown(result.DropDown, s =>
        {
            foreach (var item in dict)
            {
                if (!item.Key.Name.Contains(s)) continue;

                result.DropDown.Items.Add(GetDeconstructor(item.Value, item.Key.Name.SpaceStr()));
            }
        });

        return result;
    }

    private ToolStripMenuItem GetDeconstructor(Guid? guid, string name = "Deconstructor")
    {
        var item = new ToolStripMenuItem(name);

        if (guid.HasValue)
        {
            item.Click += (s, e) =>
            {
                var point = this.Attributes.Pivot;
                point.X += 200;

                Instances.ActiveCanvas.Document_ObjectsAdded += ModifyInput;
                Instances.ActiveCanvas.InstantiateNewObject(guid.Value, point, false);
                Instances.ActiveCanvas.Document_ObjectsAdded -= ModifyInput;

                void ModifyInput(GH_Document sender, GH_DocObjectEventArgs e)
                {
                    foreach (var item in e.Objects)
                    {
                        if (item is not IGH_Component comp) continue;
                        comp.Params.Input[0].AddSource(this);
                        this.ExpireSolution(true);
                    }
                }
            };
        }

        return item;
    }

    private ToolStripMenuItem GetCtor(Guid? guid, Dictionary<Type, Guid> dict)
    {
        var result = GetConstructor(guid);

        SimpleUtils.SearchDropdown(result.DropDown, s =>
        {
            foreach (var item in dict)
            {
                if (!item.Key.Name.Contains(s)) continue;

                result.DropDown.Items.Add(GetConstructor(item.Value, item.Key.Name.SpaceStr()));
            }
        });

        return result;
    }

    private ToolStripMenuItem GetConstructor(Guid? guid, string name = "Constructor")
    {
        var item = new ToolStripMenuItem(name);

        if (guid.HasValue)
        {
            item.Click += (s, e) =>
            {
                var point = this.Attributes.Pivot;
                point.X -= 200;

                Instances.ActiveCanvas.Document_ObjectsAdded += ModifyInput;
                Instances.ActiveCanvas.InstantiateNewObject(guid.Value, point, false);
                Instances.ActiveCanvas.Document_ObjectsAdded -= ModifyInput;

                void ModifyInput(GH_Document sender, GH_DocObjectEventArgs e)
                {
                    foreach (var item in e.Objects)
                    {
                        if (item is not IGH_Component comp) continue;
                        this.AddSource(comp.Params.Output[0]);
                        comp.ExpireSolution(true);
                    }
                }
            };
        }
        return item;
    }
}