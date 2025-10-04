using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace SecretAlliances.UIExt
{
    // 1) Try to append a button to an element with Id="Tabs"
    [PrefabExtension("ClanScreen", "descendant::*[@Id='Tabs']/Children")]
    public class ClanTabsPrefabExtension_Tabs : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child; // insert into Children
        public override int Index => -1;                     // append

        // Provide the XML via Prefabs2 content attribute (your UIExtenderEx version uses attribute-based content)
        [PrefabExtensionInsertPatch.PrefabExtensionText]
        public string ButtonXml =>
            @"<ButtonWidget WidthSizePolicy=""CoverChildren"" HeightSizePolicy=""CoverChildren"" MarginLeft=""10""
                            Command.Click=""ExecuteOpenSecretAlliances"">
                  <TextWidget WidthSizePolicy=""CoverChildren"" HeightSizePolicy=""CoverChildren""
                              Text=""{=secret_alliances_tab}Secret Alliances"" />
              </ButtonWidget>";
    }

    // 2) Fallback: some builds use Id="TabButtons"
    [PrefabExtension("ClanScreen", "descendant::*[@Id='TabButtons']/Children")]
    public class ClanTabsPrefabExtension_TabButtons : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;
        public override int Index => -1;

        [PrefabExtensionInsertPatch.PrefabExtensionText]
        public string ButtonXml =>
            @"<ButtonWidget WidthSizePolicy=""CoverChildren"" HeightSizePolicy=""CoverChildren"" MarginLeft=""10""
                            Command.Click=""ExecuteOpenSecretAlliances"">
                  <TextWidget WidthSizePolicy=""CoverChildren"" HeightSizePolicy=""CoverChildren""
                              Text=""{=secret_alliances_tab}Secret Alliances"" />
              </ButtonWidget>";
    }

    // 3) Guaranteed safety net: add a small overlay button at the top-right of ClanScreen root.
    // This path exists on every screen and ensures you can open the UI even if Tabs path differs.
    [PrefabExtension("ClanScreen", "descendant::Window/Children")]
    public class ClanTabsPrefabExtension_Overlay : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;
        public override int Index => -1;

        [PrefabExtensionInsertPatch.PrefabExtensionText]
        public string OverlayButtonXml =>
            @"<Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"" DoNotAcceptEvents=""true"">
                <Widget WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                        SuggestedWidth=""180"" SuggestedHeight=""36""
                        HorizontalAlignment=""Right"" VerticalAlignment=""Top""
                        MarginTop=""10"" MarginRight=""12"">
                  <ButtonWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                                DoNotPassEventsToChildren=""false"" DoNotAcceptEvents=""false""
                                Command.Click=""ExecuteOpenSecretAlliances"">
                    <TextWidget Text=""{=secret_alliances_tab}Secret Alliances"" />
                  </ButtonWidget>
                </Widget>
              </Widget>";
    }
}