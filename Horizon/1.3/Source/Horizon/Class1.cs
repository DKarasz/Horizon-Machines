namespace Horizon
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using HarmonyLib;
    using RimWorld;
    using UnityEngine;
    using Verse;

    public class ThingDef_AlienRace : ThingDef
    {
        public AlienSettings alienRace;

        public override void ResolveReferences()
        {
            this.comps.Add(new CompProperties(typeof(AlienPartGenerator.AlienComp)));
            base.ResolveReferences();

            if (this.alienRace.graphicPaths.NullOrEmpty())
                this.alienRace.graphicPaths.Add(new GraphicPaths());

            if (this.alienRace.generalSettings.alienPartGenerator.customHeadDrawSize == Vector2.zero)
                this.alienRace.generalSettings.alienPartGenerator.customHeadDrawSize = this.alienRace.generalSettings.alienPartGenerator.customDrawSize;
            if (this.alienRace.generalSettings.alienPartGenerator.customPortraitHeadDrawSize == Vector2.zero)
                this.alienRace.generalSettings.alienPartGenerator.customPortraitHeadDrawSize = this.alienRace.generalSettings.alienPartGenerator.customPortraitDrawSize;

            this.alienRace.graphicPaths.ForEach(action: gp =>
            {
                if (gp.customDrawSize == Vector2.one)
                    gp.customDrawSize = this.alienRace.generalSettings.alienPartGenerator.customDrawSize;
                if (gp.customPortraitDrawSize == Vector2.one)
                    gp.customPortraitDrawSize = this.alienRace.generalSettings.alienPartGenerator.customPortraitDrawSize;
                if (gp.customHeadDrawSize == Vector2.zero)
                    gp.customHeadDrawSize = this.alienRace.generalSettings.alienPartGenerator.customHeadDrawSize;
                if (gp.customPortraitHeadDrawSize == Vector2.zero)
                    gp.customPortraitHeadDrawSize = this.alienRace.generalSettings.alienPartGenerator.customPortraitHeadDrawSize;
                if (gp.headOffset == Vector2.zero)
                    gp.headOffset = this.alienRace.generalSettings.alienPartGenerator.headOffset;
                if (gp.headOffsetDirectional == null)
                    gp.headOffsetDirectional = this.alienRace.generalSettings.alienPartGenerator.headOffsetDirectional;
            });
            this.alienRace.generalSettings.alienPartGenerator.alienProps = this;


            foreach (AlienPartGenerator.BodyAddon bodyAddon in this.alienRace.generalSettings.alienPartGenerator.bodyAddons)
            {
                if (bodyAddon.offsets.west == null)
                    bodyAddon.offsets.west = bodyAddon.offsets.east;
            }

            if (this.alienRace.generalSettings.minAgeForAdulthood < 0)
                this.alienRace.generalSettings.minAgeForAdulthood = (float)AccessTools.Field(typeof(PawnBioAndNameGenerator), name: "MinAgeForAdulthood").GetValue(obj: null);

            void RecursiveAttributeCheck(Type type, Traverse instance)
            {
                if (type == typeof(ThingDef_AlienRace))
                    return;

                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    Traverse instanceNew = instance.Field(field.Name);

                    if (typeof(IList).IsAssignableFrom(field.FieldType))
                    {
                        object value = instanceNew.GetValue();
                        if (value != null)
                            foreach (object o in (IList)value)
                            {
                                if (o.GetType().Assembly == typeof(ThingDef_AlienRace).Assembly)
                                    RecursiveAttributeCheck(o.GetType(), Traverse.Create(o));
                            }
                    }

                    if (field.FieldType.Assembly == typeof(ThingDef_AlienRace).Assembly)
                        RecursiveAttributeCheck(field.FieldType, instanceNew);

                    LoadDefFromField attribute = field.GetCustomAttribute<LoadDefFromField>();
                    if (attribute != null)
                        if (instanceNew.GetValue() == null)
                            instanceNew.SetValue(attribute.GetDef(field.FieldType));
                }
            }
            RecursiveAttributeCheck(typeof(AlienSettings), Traverse.Create(this.alienRace));
        }

        public class AlienSettings
        {
            public GeneralSettings generalSettings = new GeneralSettings();
            public List<GraphicPaths> graphicPaths = new List<GraphicPaths>();
        }
    }

    public class GeneralSettings
    {
        public bool canLayDown = true;
        public float minAgeForAdulthood = -1f;
        public AlienPartGenerator alienPartGenerator = new AlienPartGenerator();
    }


    public class GraphicPaths
    {
        public List<LifeStageDef> lifeStageDefs;

        public Vector2 customDrawSize = Vector2.one;
        public Vector2 customPortraitDrawSize = Vector2.one;
        public Vector2 customHeadDrawSize = Vector2.zero;
        public Vector2 customPortraitHeadDrawSize = Vector2.zero;

        public Vector2 headOffset = Vector2.zero;
        public DirectionOffset headOffsetDirectional;

        public const string VANILLA_HEAD_PATH = "Things/Pawn/Humanlike/Heads/";
        public const string VANILLA_SKELETON_PATH = "Things/Pawn/Humanlike/HumanoidDessicated";

        public string body = "Things/Pawn/Humanlike/Bodies/";
        public string bodyMasks = string.Empty;
        private int bodyMaskCount = -1;
        public string head = "Things/Pawn/Humanlike/Heads/";
        public string headMasks = string.Empty;
        private int headMaskCount = -1;

        public string skeleton = "Things/Pawn/Humanlike/HumanoidDessicated";
        public string skull = "Things/Pawn/Humanlike/Heads/None_Average_Skull";
        public string stump = "Things/Pawn/Humanlike/Heads/None_Average_Stump";

        public ShaderTypeDef skinShader;

        public int HeadMaskCount
        {
            get
            {
                if (this.headMaskCount >= 0 || this.headMasks.NullOrEmpty())
                    return this.headMaskCount;

                this.headMaskCount = 0;
                while (ContentFinder<Texture2D>.Get($"{this.headMasks}{(this.headMaskCount == 0 ? string.Empty : this.headMaskCount.ToString())}_north", reportFailure: false) != null)
                    this.headMaskCount++;

                return this.headMaskCount;
            }
        }

        public int BodyMaskCount
        {
            get
            {
                if (this.bodyMaskCount >= 0 || this.bodyMasks.NullOrEmpty())
                    return this.bodyMaskCount;

                this.bodyMaskCount = 0;
                while (ContentFinder<Texture2D>.Get($"{this.bodyMasks}{(this.bodyMaskCount == 0 ? string.Empty : this.bodyMaskCount.ToString())}_north", reportFailure: false) != null)
                    this.bodyMaskCount++;

                return this.bodyMaskCount;
            }
        }
    }

    public class DirectionOffset
    {
        public Vector2 north = Vector2.zero;
        public Vector2 west = Vector2.zero;
        public Vector2 east = Vector2.zero;
        public Vector2 south = Vector2.zero;

        public Vector2 GetOffset(Rot4 rot) =>
            rot == Rot4.North ? this.north : rot == Rot4.East ? this.east : rot == Rot4.West ? this.west : this.south;
    }
}