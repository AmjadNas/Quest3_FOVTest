using UnityEngine;

namespace FOVTest
{
    /// <summary>The test display: uniform background, fixation target, head pointer, stimulus, response areas,
    /// answer mark and progress bar, on a sphere around the head. The sphere follows the head's position but not its
    /// rotation, so targets are world-stationary in direction (the reference headset was rotation-only).</summary>
    public class Stage
    {
        public const int Sectors = 6;
        const float LineInnerDeg = 2f, LineOuterDeg = 32f, LineWidthDeg = 0.25f;
        const float MarkScale = 1.7f;
        const float BarWidthDeg = 10f, BarHeightDeg = 0.6f, BarBelowDeg = 14f;
        const float CircleHalfWidthDeg = 0.15f;
        static readonly Color Orange = new Color(1f, 0.35f, 0f), Green = new Color(0.1f, 1f, 0.1f);

        readonly Transform root;
        readonly Camera cam;
        readonly LuminanceModel lum;
        readonly TestParameters prm;
        readonly Spot circle, ring, fixation, pointer, stimulus, barBack, barFill;
        readonly Spot[] dividers = new Spot[Sectors], mark = new Spot[2];
        float ringBrightness, lockBrightness;
        bool ringAlways;

        public Vector3 FixationDir { get; private set; } = Vector3.forward;
        public float BackgroundCd { get; private set; }
        public float BackgroundBrightness { get; private set; }

        /// <summary>What is currently on screen (checked by the tests).</summary>
        public bool ResponsePhaseShown { get; private set; }
        public Outcome? FeedbackShown { get; private set; }
        public float Progress { get; private set; }
        public bool CircleShown => circle.Visible;
        public Transform ProgressBar => barBack.Transform;
        public Transform AnswerMark => mark[0].Transform;
        public Transform Divider(int i) => dividers[i].Transform;
        public Vector3 StimulusDirection => stimulus.Transform.localPosition.normalized;

        /// <summary>Pointer and target count as connected when their discs overlap at all.</summary>
        public float ConnectRadiusDeg => (prm.FixationDotDiameterDeg + prm.PointerDiameterDeg) * 0.5f;

        public Stage(Camera cam, LuminanceModel lum, TestParameters prm, Shader spotShader)
        {
            this.cam = cam;
            this.lum = lum;
            this.prm = prm;
            root = new GameObject("Stage").transform;
            circle = new Spot("FusionCircle", root, spotShader, 0);
            for (int i = 0; i < Sectors; i++) dividers[i] = new Spot("Divider", root, spotShader, 0);
            barBack = new Spot("ProgressBack", root, spotShader, 1);
            barFill = new Spot("ProgressFill", root, spotShader, 2);
            ring = new Spot("FixationRing", root, spotShader, 1);
            fixation = new Spot("Fixation", root, spotShader, 2);
            stimulus = new Spot("Stimulus", root, spotShader, 3);
            pointer = new Spot("HeadPointer", root, spotShader, 4);
            for (int i = 0; i < mark.Length; i++) mark[i] = new Spot("AnswerMark", root, spotShader, 5);
            float d = prm.StageDistanceM;
            ring.SetShape(1.3f, 1.05f, 0.05f, d);
            fixation.SetShape(prm.FixationDotDiameterDeg * 0.5f, 0, 0.05f, d);
            pointer.SetShape(prm.PointerDiameterDeg * 0.5f, 0, 0.05f, d);
        }

        float Metres(float deg) => prm.StageDistanceM * Mathf.Tan(deg * Mathf.Deg2Rad);

        public void Configure(LayoutMeta meta, float backgroundCd)
        {
            BackgroundCd = backgroundCd;
            BackgroundBrightness = lum.BrightnessFor(backgroundCd);
            SetBackground(BackgroundBrightness);
            lockBrightness = lum.BrightnessForContrast(backgroundCd, meta.FixationDotContrast);
            ringBrightness = lum.BrightnessForContrast(backgroundCd, meta.FixationCircleContrast);
            float lines = lum.BrightnessForContrast(backgroundCd, meta.ResponseLinesContrast);
            fixation.Colour = lum.TintForContrast(backgroundCd, meta.FixationDotContrast, Orange);
            pointer.Colour = lum.TintForContrast(backgroundCd, meta.CursorContrast, Orange);
            circle.Colour = lum.TintForContrast(backgroundCd, meta.FixationCircleContrast, Green);
            barFill.Brightness = lockBrightness;
            barBack.Brightness = lines;
            foreach (var l in dividers) l.Brightness = lines;
            circle.SetShape(meta.FixedCircleRadiusDegrees + CircleHalfWidthDeg, meta.FixedCircleRadiusDegrees - CircleHalfWidthDeg,
                0.05f, prm.StageDistanceM);
        }

        public void SetBackground(float brightness) =>
            cam.backgroundColor = new Color(brightness, brightness, brightness, 1f);

        /// <summary>Keep the sphere centred on the head; call every frame.</summary>
        public void Follow(Vector3 headPosition) => root.position = headPosition;

        /// <summary>Phase 1: the target to point at. The progress bar does not move with the target: it stays level,
        /// straight below "primary", so it never looks tilted.</summary>
        public void ShowFixation(Vector3 dir, Vector3 primary, EyeMask eye, bool withRing, float progress)
        {
            var up = Vector3.up;
            FixationDir = dir.normalized;
            ringAlways = withRing;
            foreach (var s in new[] { fixation, ring, pointer, barBack, barFill }) s.Eye = eye;
            foreach (var s in dividers) s.Eye = eye;
            foreach (var s in mark) s.Eye = eye;
            circle.Eye = EyeMask.Both;       // seen by both eyes: holds binocular alignment, well outside all stimuli
            foreach (var s in new[] { fixation, ring, circle })
                s.Place(FixationDir * prm.StageDistanceM, Vector3.zero, up);
            fixation.Visible = circle.Visible = true;
            HideFeedback();
            Progress = Mathf.Clamp01(progress);
            float w = Metres(BarWidthDeg), h = Metres(BarHeightDeg);
            var barDir = VisualAngle.Direction(primary, up, 0f, -BarBelowDeg);
            barBack.PlaceOnPlane(barDir, up, prm.StageDistanceM, 0, 0, 0, w, h);
            barFill.PlaceOnPlane(barDir, up, prm.StageDistanceM, -0.5f * w * (1f - Progress), 0, 0, w * Progress, h);
            HideResponsePhase();
        }

        /// <summary>Phase 2, from the moment the target locks: a bright ring locks around the target, the circle and
        /// the progress bar go away, and lines divide the field into six areas. The lines turn with each stimulus so
        /// that it lies in the middle of an area; six-fold symmetry means they do not give away which one.</summary>
        public void ShowResponsePhase(float stimulusPolarDeg, Vector3 up)
        {
            ResponsePhaseShown = true;
            ring.Brightness = lockBrightness;
            ring.Visible = true;
            circle.Visible = false;
            barBack.Visible = barFill.Visible = false;
            float inner = Metres(LineInnerDeg), outer = Metres(LineOuterDeg), mid = 0.5f * (inner + outer);
            for (int i = 0; i < Sectors; i++)
            {
                float a = stimulusPolarDeg + 180f / Sectors + i * 360f / Sectors;
                dividers[i].PlaceOnPlane(FixationDir, up, prm.StageDistanceM,
                    mid * Mathf.Cos(a * Mathf.Deg2Rad), mid * Mathf.Sin(a * Mathf.Deg2Rad), a,
                    outer - inner, Metres(LineWidthDeg));
                dividers[i].Visible = prm.ShowDividers;
            }
        }

        void HideResponsePhase()
        {
            ResponsePhaseShown = false;
            ring.Brightness = ringBrightness;
            ring.Visible = ringAlways && fixation.Visible;
            foreach (var l in dividers) l.Visible = false;
            bool bar = prm.ShowProgress && fixation.Visible;
            barBack.Visible = bar;
            barFill.Visible = bar && Progress > 0f;
        }

        public void UpdatePointer(Vector3 headForward, Vector3 up, bool visible)
        {
            pointer.Place(headForward * prm.StageDistanceM, Vector3.zero, up);
            pointer.Visible = visible;
        }

        /// <summary>Show a stimulus at an exact angle from fixation as seen from the tested eye.
        /// Returns the grey level used.</summary>
        public float ShowStimulus(SpotDef s, Vector3 eyeWorld, Vector3 up)
        {
            var eye = eyeWorld - root.position;
            float d = prm.StageDistanceM;
            var fixFromEye = FixationDir * d - eye;
            var dir = VisualAngle.Direction(fixFromEye, up, s.X, s.Y);
            float dist = fixFromEye.magnitude;
            float brightness = lum.BrightnessForContrast(s.BackgroundCd, s.Contrast);
            stimulus.Eye = s.Eye == "Left" ? EyeMask.Left : EyeMask.Right;
            stimulus.Brightness = brightness;
            stimulus.SetShape(s.DiameterDeg * 0.5f, 0, prm.StimulusEdgeDeg, dist);
            stimulus.Place(eye + dir * dist, eye, up);
            stimulus.Visible = true;
            return brightness;
        }

        public void HideStimulus() => stimulus.Visible = false;

        /// <summary>Answer mark where the participant is pointing: green tick = right area, red cross = wrong area,
        /// grey bar = no answer. Upright in the room, whatever the head's tilt. Shape as well as colour, and dim
        /// enough not to disturb adaptation.</summary>
        public void ShowFeedback(Outcome outcome, Vector3 dir)
        {
            var up = Vector3.up;
            FeedbackShown = outcome;
            // The luminance of the fixation dot (or darker than a bright background), tinted.
            bool dark = BackgroundCd < 10f;
            float v = dark ? lockBrightness : lum.BrightnessForContrast(BackgroundCd, -0.5f);
            float low = dark ? 0.25f * v : 0f;
            var colour = outcome == Outcome.Seen ? new Color(low, v, low)
                       : outcome == Outcome.Incorrect ? new Color(v, low, low) : new Color(v, v, v);
            float d = prm.StageDistanceM, t = Metres(0.35f * MarkScale);
            void Stroke(int i, float x, float y, float roll, float length)
            {
                mark[i].Colour = colour;
                mark[i].PlaceOnPlane(dir, up, d, Metres(x * MarkScale), Metres(y * MarkScale), roll, Metres(length * MarkScale), t);
                mark[i].Visible = true;
            }
            mark[1].Visible = false;
            switch (outcome)
            {
                case Outcome.Seen:
                    Stroke(0, -0.65f, -0.25f, -45f, 1.1f);
                    Stroke(1, 0.45f, 0.1f, 50f, 2.1f);
                    break;
                case Outcome.Incorrect:
                    Stroke(0, 0, 0, 45f, 2.2f);
                    Stroke(1, 0, 0, -45f, 2.2f);
                    break;
                default:
                    Stroke(0, 0, 0, 0, 2f);
                    break;
            }
        }

        void HideFeedback()
        {
            FeedbackShown = null;
            foreach (var m in mark) m.Visible = false;
        }

        /// <summary>Between trials only the circle (and the answer mark) is shown.</summary>
        public void HideTarget()
        {
            fixation.Visible = stimulus.Visible = false;
            HideResponsePhase();
            circle.Visible = true;
        }

        public void HideAll()
        {
            HideTarget();
            HideFeedback();
            pointer.Visible = circle.Visible = false;
        }
    }
}
