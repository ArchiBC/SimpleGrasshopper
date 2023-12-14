﻿using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using SimpleGrasshopper.Attributes;
using SimpleGrasshopper.Data;

namespace SimpleGrasshopper.GHTests;

[SubCategory("Just a test")]
internal class SimpleSubcategory
{
    [Icon("ConstructRenderItemComponent_24-24.png")] // The name of the png that is embedded in your dll.
    [Exposure(GH_Exposure.secondary)]
    [DocObj("Addition", "Add", "The addition of the integers.")]
    [return: DocObj("Result", "R", "Result")]
    private static int SimpleMethod(
        [Range(0, 10)] List<int> a,
        [Range(0, 5)] GH_Structure<GH_Integer> b, ref int c)
    {
        SettingClass.EnumTest = EnumTesting.Why;
        c = 11 + c;
        return 11;
    }

    [DocObj("Addition2", "Add2", "The addition of the integers2.")]
    private static void SimpleMethod(double abc, double werb, out double dfa)
    {
        dfa = abc + werb;
    }

    [Icon("https://raw.githubusercontent.com/ArchiDog1998/WatermarkPainter/master/WatermarkIcon512.png")]
    [DocObj("Special Param", "Spe", "Special Params")]
    private static void ParamTest(
        [DocObj("Name", "N", "The name of sth.")] string name,
        [Param(ParamGuids.FilePath)] string path,
        [Angle] out double angle)
    {
        angle = Math.PI;
    }

    [DocObj("Enum Param", "Enu", "Enum testing")]
    private static void EnumTypeTest(out EnumTest type, EnumTest input = EnumTest.First)
    {
        type = EnumTest.First;
    }

    [DocObj("Type Testing", "T", "Testing for my type")]
    private static void MyTypeTest(GH_Structure<SimpleGoo<TypeTest>> type, GH_Structure<GH_Boolean> bools)
    {

    }

    [Exposure(GH_Exposure.secondary)]
    [DocObj("Test", "T", "Ttt")]

    private static void Test(bool[] bs, bool a, out bool[] re)
    {
        re = [true, false, a];
    }

    [DocObj("Test2", "T2", "Ttt")]

    private static void Test(out bool a)
    {
        a = false;
    }

    [Message("Great")]
    [DocObj("BendLine", "SM-Unfold", "")]
    public static RuntimeData BendL(
           [DocObj("Brep", "B", "Input Brep")] Brep brep,
           [DocObj("BendLines", "L", "BendLines for the Brep")] out List<Curve> BL,
           [DocObj("Indices", "I", "Indices of the bend Srf")] out List<int> ID)

    {
        double t = 0.5;

        List<Curve> bendLines = [];
        List<int> indices = [];

        if (brep == null || brep.Faces.Count == 0)
        {
            throw new Exception("Invalid input Brep");
        }

        for (int i = 0; i < brep.Faces.Count; i++)
        {
            BrepFace srf = brep.Faces[i];
            if (!srf.IsPlanar(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance))
            {
                srf.SetDomain(0, new Interval(0, 1));
                srf.SetDomain(1, new Interval(0, 1));

                for (int d = 0; d <= 1; d++) // Iterate over both directions
                {
                    Curve[] curves = srf.TrimAwareIsoCurve(d, t);
                    foreach (Curve curve in curves)
                    {
                        if (curve.IsLinear())
                        {
                            bendLines.Add(curve);
                            indices.Add(i);
                        }
                    }
                }
            }
        }
        BL = bendLines;
        ID = indices;

        return new RuntimeData(null, [new RuntimeMessage(GH_RuntimeMessageLevel.Warning, "just a test")]);
    }
}

partial class SimpleSubcategory_Test_Component
{
    public override IGH_Attributes CreateAttribute()
    {
        return base.CreateAttribute();
    }
}

public enum EnumTest : byte
{
    First,
    Second,
}
