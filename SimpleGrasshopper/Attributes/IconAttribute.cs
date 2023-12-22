﻿namespace SimpleGrasshopper.Attributes;

/// <summary>
/// For adding the <see cref="IGH_DocumentObject.Icon_24x24"/> to the <see cref="IGH_DocumentObject"/>
/// Please don't forget to embed your file into your project!
/// </summary>
/// <param name="iconPath">the path or name of the icon that is embedded in your project</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]

public class IconAttribute(string iconPath) : Attribute
{
    /// <summary>
    /// 
    /// </summary>
    public string IconPath => iconPath;
}
