using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MM.SpriteAnimation
{
    internal sealed class SpriteAnimationPreview : VisualElement
    {
        private const long TickMilliseconds = 15;
        private const string Separator = " \u00B7 ";
        private const float EventLabelMargin = 6f;
        private const float EventLabelPadding = 4f;
        private static readonly Color Background = new Color(0.16f, 0.16f, 0.16f);
        private static readonly Color EventLabelBackground = new Color(0f, 0f, 0f, 0.6f);

        private readonly IMGUIContainer canvas;
        private readonly Button playButton;
        private readonly SliderInt frameSlider;
        private readonly Label frameLabel;
        private readonly Toggle loopToggle;
        private readonly Toggle flipToggle;
        private readonly Slider zoomSlider;
        private readonly Label message;
        private readonly IVisualElementScheduledItem ticker;

        private SpriteAnimationProfile profile;
        private AnimationSource source;
        private string loadedKey;
        private Sprite[][] layers;
        private bool[] optionalLayers;
        private ReferencePose[] poses;
        private Sprite anchor;
        private Vector2 canvasSize;
        private Vector2 bodyPivot;
        private int frame;
        private bool playing;
        private double playStartTime;
        private ClipTiming timing;
        private string[] frameEvents;
        private int playStartTick;

        internal SpriteAnimationPreview()
        {
            AddToClassList("sab-preview");

            var playback = new VisualElement();
            playback.AddToClassList("sab-preview-controls");
            var display = new VisualElement();
            display.AddToClassList("sab-preview-controls");
            playButton = new Button(TogglePlay) { text = "Play" };
            frameSlider = new SliderInt(0, 0);
            frameSlider.AddToClassList("sab-preview-slider");
            frameSlider.RegisterValueChangedCallback(evt =>
            {
                Pause();
                SetFrame(evt.newValue);
            });
            frameLabel = new Label("0/0");
            frameLabel.AddToClassList("sab-preview-frame");
            loopToggle = new Toggle { text = "Loop" };
            loopToggle.AddToClassList("sab-preview-flip");
            flipToggle = new Toggle { text = "Flip" };
            flipToggle.AddToClassList("sab-preview-flip");
            flipToggle.RegisterValueChangedCallback(_ => canvas.MarkDirtyRepaint());
            zoomSlider = new Slider("Zoom", 0.25f, 4f) { value = 1f };
            zoomSlider.AddToClassList("sab-preview-zoom");
            zoomSlider.RegisterValueChangedCallback(_ => canvas.MarkDirtyRepaint());

            playback.Add(playButton);
            playback.Add(new Button(() => Step(-1)) { text = "<" });
            playback.Add(new Button(() => Step(1)) { text = ">" });
            playback.Add(frameSlider);
            playback.Add(frameLabel);
            display.Add(loopToggle);
            display.Add(flipToggle);
            display.Add(zoomSlider);
            Add(playback);
            Add(display);

            message = new Label();
            message.AddToClassList("sab-preview-message");
            Add(message);

            canvas = new IMGUIContainer(Draw);
            canvas.AddToClassList("sab-preview-canvas");
            Add(canvas);

            ticker = schedule.Execute(Tick).Every(TickMilliseconds);
            ticker.Pause();
            RegisterCallback<DetachFromPanelEvent>(_ => Pause());
            Load();
        }

        private int FrameCount => layers != null && layers.Length > 0 ? layers[0].Length : 0;

        internal void Show(SpriteAnimationProfile target, AnimationSource selected)
        {
            if (target != null && target != profile) flipToggle.SetValueWithoutNotify(target.FlipX);
            profile = target;
            source = selected;

            string key = target == null || selected == null ? null : $"{AssetDatabase.GetAssetPath(target)}|{selected.Name}|{selected.Status}|{selected.Fingerprint}";
            if (key == loadedKey)
            {
                RefreshTiming();
                return;
            }
            loadedKey = key;
            Load();
        }

        private void Load()
        {
            Pause();
            layers = null;
            poses = null;
            anchor = null;
            if (profile != null && source != null) loopToggle.SetValueWithoutNotify(profile.LoopOf(source.Name));

            string problem = LoadFrames();
            message.text = problem ?? string.Empty;
            message.style.display = problem != null ? DisplayStyle.Flex : DisplayStyle.None;
            frameSlider.highValue = Mathf.Max(0, FrameCount - 1);
            frameSlider.SetEnabled(FrameCount > 1);
            RefreshTiming();
            SetFrame(0);
        }

        private void RefreshTiming()
        {
            timing = null;
            frameEvents = null;
            if (profile != null && source != null && FrameCount > 0)
            {
                timing = SpriteAnimationScanner.EffectiveTiming(profile, source, out ClipTiming clipTiming, out _);
                if (timing == null || timing.FrameCount != FrameCount) timing = ClipTiming.Uniform(Mathf.Max(1, profile.FrameRateOf(source.Name)), FrameCount, profile.MinTickRate);
                else if (clipTiming != null) frameEvents = EventsByFrame(clipTiming);
            }
            UpdateFrameLabel();
            canvas.MarkDirtyRepaint();
        }

        private string[] EventsByFrame(ClipTiming clipTiming)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(profile.ClipPath(source.Name));
            if (clip == null) return null;

            var names = new string[FrameCount];
            foreach (AnimationEvent animationEvent in AnimationUtility.GetAnimationEvents(clip))
            {
                int index = clipTiming.FrameAt(animationEvent.time, out _);
                if (index >= names.Length) continue;
                names[index] = names[index] == null ? animationEvent.functionName : $"{names[index]}, {animationEvent.functionName}";
            }
            return names;
        }

        private string LoadFrames()
        {
            if (profile == null || source == null) return "Select an animation to preview.";
            if (!source.HasFolder) return "No art for this animation.";
            if (source.Problems.Count > 0) return "Fix the errors to preview.";

            layers = source.Parts.Select(part => part.Frames.Select(path => path != null ? AssetDatabase.LoadAssetAtPath<Sprite>(path) : null).ToArray()).ToArray();
            optionalLayers = source.Parts.Select(part => part.Part.Optional).ToArray();
            bool notImported = source.Parts.Where((part, layer) => part.Frames.Where((path, index) => path != null && layers[layer][index] == null).Any()).Any();
            if (notImported)
            {
                layers = null;
                return "Some frames are not imported as sprites yet; build once to import them.";
            }

            Sprite first = layers[0][0];
            canvasSize = first.rect.size;
            bodyPivot = first.pivot / first.rect.size;
            poses = ReferenceTracking.PreviewPoses(profile, source, bodyPivot, out anchor, out string problem);
            return problem;
        }

        private void TogglePlay()
        {
            if (playing) Pause();
            else Play();
        }

        private void Play()
        {
            if (FrameCount < 2) return;
            RefreshTiming();
            if (!loopToggle.value && frame == FrameCount - 1) SetFrame(0);
            playing = true;
            playStartTime = EditorApplication.timeSinceStartup;
            playStartTick = timing.StartTick(frame);
            playButton.text = "Pause";
            ticker.Resume();
        }

        private void Pause()
        {
            playing = false;
            playButton.text = "Play";
            ticker.Pause();
        }

        private void Step(int delta)
        {
            Pause();
            int count = FrameCount;
            if (count > 0) SetFrame(((frame + delta) % count + count) % count);
        }

        private void Tick()
        {
            if (!playing || timing == null || FrameCount == 0 || timing.TotalTicks == 0) return;
            int tick = playStartTick + (int)((EditorApplication.timeSinceStartup - playStartTime) * timing.TickRate);
            if (!loopToggle.value && tick >= timing.TotalTicks)
            {
                SetFrame(FrameCount - 1);
                Pause();
                return;
            }
            int next = timing.FrameAtTick(tick % timing.TotalTicks);
            if (next != frame) SetFrame(next);
        }

        private void SetFrame(int value)
        {
            int count = FrameCount;
            frame = count == 0 ? 0 : Mathf.Clamp(value, 0, count - 1);
            frameSlider.SetValueWithoutNotify(frame);
            UpdateFrameLabel();
            canvas.MarkDirtyRepaint();
        }

        private void UpdateFrameLabel()
        {
            int count = FrameCount;
            if (count == 0)
            {
                frameLabel.text = "0/0";
                return;
            }

            string text = $"{frame + 1}/{count}";
            if (timing != null && frame < timing.FrameCount)
            {
                int ticks = timing.Ticks(frame);
                text += $"{Separator}{ticks} tick{(ticks == 1 ? string.Empty : "s")}{Separator}{timing.Duration(frame).ToString("0.###", CultureInfo.InvariantCulture)} s";
            }
            frameLabel.text = text;
        }

        private void Draw()
        {
            Rect area = canvas.contentRect;
            if (Event.current.type != EventType.Repaint || area.width <= 0f || area.height <= 0f) return;

            EditorGUI.DrawRect(area, Background);
            if (FrameCount == 0) return;

            float scale = Mathf.Min(area.width / canvasSize.x, area.height / canvasSize.y) * zoomSlider.value;
            Vector2 origin = area.center + new Vector2((bodyPivot.x - 0.5f) * canvasSize.x, (0.5f - bodyPivot.y) * canvasSize.y) * scale;
            bool flip = flipToggle.value;

            DrawLayers(false, origin, scale, flip);

            if (anchor != null && poses != null && frame < poses.Length && poses[frame].Visible)
            {
                ReferencePose pose = poses[frame];
                Vector2 point = origin + new Vector2(pose.Position.x, -pose.Position.y) * (profile.PixelsPerUnit * scale);
                DrawSprite(anchor, point, scale, pose.Angle, origin, flip);
            }

            DrawLayers(true, origin, scale, flip);
            DrawEventLabel(area);
        }

        private void DrawEventLabel(Rect area)
        {
            if (frameEvents == null || frame >= frameEvents.Length || frameEvents[frame] == null) return;

            var content = new GUIContent(frameEvents[frame]);
            Vector2 size = EditorStyles.boldLabel.CalcSize(content);
            var box = new Rect(area.x + EventLabelMargin, area.y + EventLabelMargin, size.x + EventLabelPadding * 2f, size.y + EventLabelPadding);
            EditorGUI.DrawRect(box, EventLabelBackground);
            GUI.Label(new Rect(box.x + EventLabelPadding, box.y + EventLabelPadding * 0.5f, size.x, size.y), content, EditorStyles.boldLabel);
        }

        private void DrawLayers(bool optional, Vector2 origin, float scale, bool flip)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                if (optionalLayers[i] == optional) DrawSprite(layers[i][frame], origin, scale, 0f, origin, flip);
            }
        }

        private static void DrawSprite(Sprite sprite, Vector2 pivotPoint, float scale, float angle, Vector2 origin, bool flip)
        {
            if (sprite == null) return;

            Matrix4x4 saved = GUI.matrix;
            if (flip) GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), origin);
            if (angle != 0f) GUIUtility.RotateAroundPivot(flip ? angle : -angle, flip ? new Vector2(2f * origin.x - pivotPoint.x, pivotPoint.y) : pivotPoint);

            Rect rect = sprite.rect;
            Texture2D texture = sprite.texture;
            var screen = new Rect(pivotPoint.x - sprite.pivot.x * scale, pivotPoint.y - (rect.height - sprite.pivot.y) * scale, rect.width * scale, rect.height * scale);
            var uv = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
            GUI.DrawTextureWithTexCoords(screen, texture, uv, true);
            GUI.matrix = saved;
        }
    }
}
