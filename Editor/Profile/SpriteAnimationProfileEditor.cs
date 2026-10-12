using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MM.SpriteAnimation
{
    [CustomEditor(typeof(SpriteAnimationProfile))]
    internal sealed class SpriteAnimationProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty controller;
        private SerializedProperty layer;
        private SerializedProperty multiPart;
        private SerializedProperty parts;
        private SerializedProperty pivot;
        private SerializedProperty tickGrid;
        private SerializedProperty trackingEnabled;

        private DropdownField layerField;
        private PropertyField createMissingField;
        private PropertyField rendererPathField;
        private PropertyField partsField;
        private PropertyField customPivotField;
        private PropertyField anchorSpriteField;
        private PropertyField minTickRateField;
        private VisualElement trackingBody;

        public override VisualElement CreateInspectorGUI()
        {
            controller = serializedObject.FindProperty("controller");
            layer = serializedObject.FindProperty("layer");
            multiPart = serializedObject.FindProperty("multiPart");
            parts = serializedObject.FindProperty("parts");
            pivot = serializedObject.FindProperty("pivot");
            tickGrid = serializedObject.FindProperty("tickGrid");
            trackingEnabled = serializedObject.FindProperty("tracking.enabled");

            var root = new VisualElement();

            VisualElement general = Section(root, "General");
            general.Add(Field("displayName"));
            general.Add(Field("spriteRoot"));
            general.Add(Field("clipRoot"));
            general.Add(Field("controller"));
            layerField = new DropdownField("Layer");
            layerField.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            layerField.RegisterValueChangedCallback(OnLayerChanged);
            general.Add(layerField);
            general.Add(Field("clipPrefix"));
            general.Add(Field("filePrefix"));
            general.Add(createMissingField = Field("createMissingStates"));
            general.Add(Field("defaultFrameRate"));

            VisualElement sprites = Section(root, "Sprites");
            sprites.Add(Field("pixelsPerUnit"));
            sprites.Add(Field("flipX"));
            sprites.Add(Field("pivot"));
            sprites.Add(customPivotField = Field("customPivot"));
            sprites.Add(anchorSpriteField = Field("anchorSprite"));

            VisualElement partsSection = Section(root, "Parts");
            partsSection.Add(Field("multiPart"));
            partsSection.Add(rendererPathField = new PropertyField { label = "Renderer Path" });
            partsSection.Add(partsField = Field("parts"));

            VisualElement import = Section(root, "Import");
            import.Add(Field("import.filterMode"));
            import.Add(Field("import.compression"));
            import.Add(Field("import.meshType"));
            import.Add(Field("import.maxTextureSize"));

            VisualElement timing = Section(root, "Timing");
            timing.Add(Field("tickGrid"));
            timing.Add(minTickRateField = Field("minTickRate"));
            timing.Add(Field("eventSync"));

            VisualElement tracking = Section(root, "Reference Tracking");
            tracking.Add(Field("tracking.enabled", "Enabled"));
            trackingBody = new VisualElement();
            trackingBody.Add(Field("tracking.token"));
            trackingBody.Add(Field("tracking.targetPath"));
            trackingBody.Add(Field("tracking.trackedProfile"));
            trackingBody.Add(Field("tracking.snapToPixel"));
            trackingBody.Add(Field("tracking.rotationDeadZone"));
            tracking.Add(trackingBody);

            var open = new Button(() => SpriteAnimationWindow.Open((SpriteAnimationProfile)target)) { text = "Open in Sprite Animation Builder" };
            open.style.marginTop = 8f;
            root.Add(open);

            foreach (SerializedProperty property in new[] { controller, layer, multiPart, parts, pivot, tickGrid, trackingEnabled })
            {
                root.TrackPropertyValue(property, _ => UpdateVisibility());
            }
            UpdateVisibility();
            return root;
        }

        private void UpdateVisibility()
        {
            serializedObject.Update();

            bool multi = multiPart.boolValue;
            Show(partsField, multi);
            Show(rendererPathField, !multi);
            if (!multi && parts.arraySize > 0)
            {
                SerializedProperty rendererPath = parts.GetArrayElementAtIndex(0).FindPropertyRelative("rendererPath");
                if (rendererPathField.bindingPath != rendererPath.propertyPath) rendererPathField.BindProperty(rendererPath);
            }

            Show(customPivotField, pivot.intValue == (int)SpritePivotMode.Custom);
            Show(anchorSpriteField, pivot.intValue == (int)SpritePivotMode.Anchor);
            Show(minTickRateField, tickGrid.boolValue);
            Show(trackingBody, trackingEnabled.boolValue);

            var asset = controller.objectReferenceValue as RuntimeAnimatorController;
            Show(createMissingField, asset is AnimatorController);
            RebuildLayerChoices(asset);
        }

        private void RebuildLayerChoices(RuntimeAnimatorController asset)
        {
            AnimatorController baseController = asset != null ? ControllerBinding.BaseOf(asset) : null;
            if (baseController == null || baseController.layers.Length == 0)
            {
                Show(layerField, false);
                return;
            }

            List<string> names = baseController.layers.Select(item => item.name).ToList();
            layerField.choices = names;
            layerField.SetValueWithoutNotify(string.IsNullOrEmpty(layer.stringValue) ? names[0] : layer.stringValue);
            Show(layerField, true);
        }

        private void OnLayerChanged(ChangeEvent<string> evt)
        {
            int index = layerField.choices.IndexOf(evt.newValue);
            layer.stringValue = index <= 0 ? string.Empty : evt.newValue;
            serializedObject.ApplyModifiedProperties();
        }

        private PropertyField Field(string path, string label = null)
        {
            SerializedProperty property = serializedObject.FindProperty(path);
            return label == null ? new PropertyField(property) : new PropertyField(property, label);
        }

        private static VisualElement Section(VisualElement root, string title)
        {
            var header = new Label(title);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 8f;
            header.style.marginLeft = 3f;
            root.Add(header);

            var body = new VisualElement();
            root.Add(body);
            return body;
        }

        private static void Show(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
