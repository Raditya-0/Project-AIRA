using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;            // CubismEyeBlinkController
using Live2D.Cubism.Framework.Expression; // CubismExpressionController
using Live2D.Cubism.Framework.MouthMovement; // CubismMouthController
using UnityEngine;

public class AiraController : MonoBehaviour
{
    // Singleton 
    public static AiraController Instance { get; private set; }

    // Inspector References 
    [Header("Live2D Components")]
    [SerializeField] private CubismModel                _cubismModel;
    [SerializeField] private CubismEyeBlinkController   _eyeBlinkController;
    [SerializeField] private CubismMouthController      _mouthController;
    [SerializeField] private CubismExpressionController _expressionController;

    [Header("Breathing")]
    [SerializeField] private float _breathPeriodSeconds = 3f;

    [Header("Expression Transition Settings")]
    [SerializeField] private float _lerpInDuration = 0.8f;
    [SerializeField] private float _lerpOutDuration = 1.0f;
    [SerializeField] private float _holdDuration = 5.0f;

    [Header("Expression Intensity")]
    [Range(0f, 1f)]
    [SerializeField] private float _expressionIntensity = 1.0f;

    [Header("Body Expression Multiplier")]
    [Range(0f, 1f)]
    [SerializeField] private float _bodyReactionIntensity = 0.7f;

    [Header("Face Expression Values")]
    [SerializeField] private float _sadEyeOpen        = 0.3f;
    [SerializeField] private float _thinkingEyeBallY  =  0.8f;
    [SerializeField] private float _shakeIntensity    = 0.05f;
    [SerializeField] private float _shakeDuration     = 0.3f;

    [Header("Body Loop Speeds")]
    [SerializeField] private float _happyBounceSpeed  = 0.6f;
    [SerializeField] private float _sadSwaySpeed      = 2.5f;
    [SerializeField] private float _thinkingLeanSpeed = 4.0f;
    [SerializeField] private float _shyWiggleSpeed    = 1.5f;

    [Header("Body Loop Intensity")]
    [SerializeField] private float _happyBounceAmt = 3f;
    [SerializeField] private float _sadHangAmt     = 5f;
    [SerializeField] private float _shyTiltAmt     = 10f;

    [Header("Body Loop Duration")]
    [SerializeField] private float _happyBodyDuration    = 4f;
    [SerializeField] private float _sadBodyDuration      = 6f;
    [SerializeField] private float _surprisedBodyDuration = 4f;
    [SerializeField] private float _thinkingBodyDuration = -1f;
    [SerializeField] private float _shyBodyDuration      = 5f;

    // Parameter ID Constants
    private const string P_EYE_L_OPEN   = "ParamEyeLOpen";
    private const string P_EYE_R_OPEN   = "ParamEyeROpen";
    private const string P_EYE_L_SMILE  = "ParamEyeLSmile";
    private const string P_EYE_R_SMILE  = "ParamEyeRSmile";
    private const string P_EYEBALL_X    = "ParamEyeBallX";
    private const string P_EYEBALL_Y    = "ParamEyeBallY";
    private const string P_MOUTH_OPEN   = "ParamMouthOpenY";
    private const string P_MOUTH_FORM   = "ParamMouthForm";
    private const string P_ANGLE_X      = "ParamAngleX";
    private const string P_ANGLE_Y      = "ParamAngleY";
    private const string P_ANGLE_Z      = "ParamAngleZ";
    private const string P_BREATH       = "ParamBreath";
    private const string P_FLUSH        = "ParamCheek";
    private const string P_BROW_L_FORM  = "ParamBrowLForm";
    private const string P_BROW_R_FORM  = "ParamBrowRForm";
    private const string P_BODY_ANGLE_X = "ParamBodyAngleX";
    private const string P_BODY_ANGLE_Y = "ParamBodyAngleY";
    private const string P_BODY_ANGLE_Z = "ParamBodyAngleZ";

    // Private State
    private Coroutine _breathingCoroutine;
    private Coroutine _expressionCoroutine;
    private Coroutine _bodyMoodCoroutine;
    private Coroutine _shakeCoroutine;
    private float   _idleSwayMultiplier = 1.0f;
    private Vector3 _modelOriginalPos;

    // Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        ValidateComponents();
        EnsureEyeBlinkActive();
        if (_cubismModel != null)
            _modelOriginalPos = _cubismModel.transform.localPosition;
        StartBreathing();
        StartIdleAnimation();
    }

    // Public API
    public void SetExpression(string tag)
    {
        if (string.IsNullOrEmpty(tag)) tag = "[NEUTRAL]";
        string name = tag.Trim().ToUpper().Trim('[', ']');

        if (TrySetExpressionByName(name))
        {
            Debug.Log($"[AiraController] Expression via ExpressionController → {name}");
            return;
        }

        if (_shakeCoroutine != null)
        {
            StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = null;
            if (_cubismModel != null)
                _cubismModel.transform.localPosition = _modelOriginalPos;
        }

        if (_expressionCoroutine != null)
        {
            StopCoroutine(_expressionCoroutine);
            _expressionCoroutine = null;
        }

        if (_bodyMoodCoroutine != null)
        {
            StopCoroutine(_bodyMoodCoroutine);
            _bodyMoodCoroutine = null;
        }

        _idleSwayMultiplier = (name == "NEUTRAL") ? 1.0f : 0.2f;

        _expressionCoroutine = StartCoroutine(SmoothExpressionCoroutine(name));
        _bodyMoodCoroutine   = StartCoroutine(GetBodyLoopForTag(name));
        Debug.Log($"[AiraController] Expression via parameters → {name}");
    }

    public void SetMouthOpen(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (_mouthController != null)
            _mouthController.MouthOpening = clamped;
        SetParam(P_MOUTH_OPEN, clamped);
    }

    public void StartIdleAnimation()
    {
        StartBreathing();
        Debug.Log("[AiraController] Idle animation started.");
    }

    // Tag Parsing (Static Utilities)
    public static string StripExpressionTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Regex.Replace(
            text,
            @"\[(HAPPY|SAD|SURPRISED|THINKING|SHY|NEUTRAL)\]\s*",
            string.Empty,
            RegexOptions.IgnoreCase
        ).Trim();
    }

    public static string ExtractExpressionTag(string text)
    {
        if (string.IsNullOrEmpty(text)) return "[NEUTRAL]";
        var match = Regex.Match(
            text,
            @"\[(HAPPY|SAD|SURPRISED|THINKING|SHY|NEUTRAL)\]",
            RegexOptions.IgnoreCase
        );
        return match.Success ? match.Value.ToUpper() : "[NEUTRAL]";
    }

    // Expression Implementation
    private bool TrySetExpressionByName(string name)
    {
        if (_expressionController == null) return false;
        var list = _expressionController.ExpressionsList;
        if (list == null || list.CubismExpressionObjects == null
            || list.CubismExpressionObjects.Length == 0)
            return false;
        var expressions = list.CubismExpressionObjects;
        for (int i = 0; i < expressions.Length; i++)
        {
            if (expressions[i] == null) continue;
            if (expressions[i].name.ToUpper().Contains(name))
            {
                _expressionController.CurrentExpressionIndex = i;
                return true;
            }
        }
        return false;
    }

    // Smooth Expression Coroutine (FACE params)
    private IEnumerator SmoothExpressionCoroutine(string name)
    {
        bool isNeutral = (name == "NEUTRAL");

        if (name == "SURPRISED")
            _shakeCoroutine = StartCoroutine(ShakeCoroutine(_shakeIntensity, _shakeDuration));

        (string id, float target)[] rawTargets = GetExpressionTargets(name);
        float duration = GetExpressionDuration(name);

        float[] effectiveTargets = new float[rawTargets.Length];
        for (int i = 0; i < rawTargets.Length; i++)
            effectiveTargets[i] = ApplyIntensity(rawTargets[i].id, rawTargets[i].target);

        float[] startValues = new float[rawTargets.Length];
        for (int i = 0; i < rawTargets.Length; i++)
            startValues[i] = GetParam(rawTargets[i].id);

        float elapsed = 0f;
        while (elapsed < _lerpInDuration)
        {
            elapsed += Time.deltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _lerpInDuration));
            for (int i = 0; i < rawTargets.Length; i++)
                SetParam(rawTargets[i].id, Mathf.Lerp(startValues[i], effectiveTargets[i], smooth));
            yield return null;
        }
        for (int i = 0; i < rawTargets.Length; i++)
            SetParam(rawTargets[i].id, effectiveTargets[i]);

        if (isNeutral)
        {
            _expressionCoroutine = null;
            yield break;
        }

        if (duration < 0f)
            yield break;

        yield return new WaitForSeconds(duration);

        (string id, float target)[] neutrals = GetNeutralTargets();
        float[] holdValues = new float[neutrals.Length];
        for (int i = 0; i < neutrals.Length; i++)
            holdValues[i] = GetParam(neutrals[i].id);

        elapsed = 0f;
        while (elapsed < _lerpOutDuration)
        {
            elapsed += Time.deltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _lerpOutDuration));
            for (int i = 0; i < neutrals.Length; i++)
                SetParam(neutrals[i].id, Mathf.Lerp(holdValues[i], neutrals[i].target, smooth));
            yield return null;
        }
        for (int i = 0; i < neutrals.Length; i++)
            SetParam(neutrals[i].id, neutrals[i].target);
        _expressionCoroutine = null;
    }

    // Shake Coroutine
    private IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-intensity, intensity);
            float y = Random.Range(-intensity, intensity);
            if (_cubismModel != null)
                _cubismModel.transform.localPosition = _modelOriginalPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (_cubismModel != null)
            _cubismModel.transform.localPosition = _modelOriginalPos;
        _shakeCoroutine = null;
    }

    // Expression Data (face params only)
    private float GetExpressionDuration(string name)
    {
        switch (name)
        {
            case "SURPRISED": return 3f;
            case "SAD":       return 6f;
            case "SHY":       return 7f;
            case "THINKING":  return -1f;
            default:          return _holdDuration; 
        }
    }

    private (string id, float target)[] GetExpressionTargets(string name)
    {
        switch (name)
        {
            case "HAPPY":
                return new[]
                {
                    (P_EYE_L_OPEN,  1f),
                    (P_EYE_R_OPEN,  1f),
                    (P_EYE_L_SMILE, 1f),
                    (P_EYE_R_SMILE, 1f),
                    (P_MOUTH_FORM,  1f),
                    (P_BROW_L_FORM, -0.5f),
                    (P_BROW_R_FORM, -0.5f),
                };

            case "SAD":
                return new[]
                {
                    (P_EYE_L_OPEN,  _sadEyeOpen),
                    (P_EYE_R_OPEN,  _sadEyeOpen),
                    (P_MOUTH_FORM,  -1f),
                    (P_BROW_L_FORM,  0.8f),
                    (P_BROW_R_FORM,  0.8f),
                    (P_EYEBALL_Y,   -0.8f),
                };

            case "SURPRISED":
                return new[]
                {
                    (P_EYE_L_OPEN,  1f),
                    (P_EYE_R_OPEN,  1f),
                    (P_BROW_L_FORM, -1f),
                    (P_BROW_R_FORM, -1f),
                    (P_MOUTH_FORM,  -0.8f),
                };

            case "THINKING":
                return new[]
                {
                    (P_EYE_L_OPEN,  1f),
                    (P_EYE_R_OPEN,  1f),
                    (P_EYEBALL_Y,   _thinkingEyeBallY),
                    (P_EYEBALL_X,  -0.5f),
                    (P_BROW_L_FORM,  0.5f),
                    (P_BROW_R_FORM, -0.5f),
                    (P_MOUTH_FORM,  -0.3f),
                };

            case "SHY":
                return new[]
                {
                    (P_EYE_L_OPEN,  1f),
                    (P_EYE_R_OPEN,  1f),
                    (P_FLUSH,       1f),
                    (P_EYEBALL_Y,  -1f),
                    (P_MOUTH_FORM,  0.5f),
                };

            default: // NEUTRAL
                return GetNeutralTargets();
        }
    }

    private static (string id, float target)[] GetNeutralTargets() => new[]
    {
        (P_EYE_L_OPEN,  1f),
        (P_EYE_R_OPEN,  1f),
        (P_EYE_L_SMILE, 0f),
        (P_EYE_R_SMILE, 0f),
        (P_MOUTH_FORM,  0f),
        (P_BROW_L_FORM, 0f),
        (P_BROW_R_FORM, 0f),
        (P_FLUSH,       0f),
        (P_EYEBALL_X,   0f),
        (P_EYEBALL_Y,   0f),
    };

    // Intensity Application
    private float ApplyIntensity(string paramId, float target)
    {
        float neutral   = (paramId == P_EYE_L_OPEN || paramId == P_EYE_R_OPEN) ? 1f : 0f;
        float intensity = IsBodyParam(paramId) ? _bodyReactionIntensity : _expressionIntensity;
        return neutral + (target - neutral) * intensity;
    }

    private static bool IsBodyParam(string paramId)
    {
        return paramId == P_BODY_ANGLE_X
            || paramId == P_BODY_ANGLE_Y
            || paramId == P_BODY_ANGLE_Z
            || paramId == P_ANGLE_X
            || paramId == P_ANGLE_Y
            || paramId == P_ANGLE_Z;
    }

    // Body Mood Loop Dispatch
    private IEnumerator GetBodyLoopForTag(string name)
    {
        return name switch
        {
            "HAPPY"     => HappyBodyLoop(_happyBodyDuration),
            "SAD"       => SadBodyLoop(_sadBodyDuration),
            "SURPRISED" => SurprisedBodyLoop(_surprisedBodyDuration),
            "THINKING"  => ThinkingBodyLoop(_thinkingBodyDuration),
            "SHY"       => ShyBodyLoop(_shyBodyDuration),
            _           => NeutralBodyReturn(),
        };
    }

    // Body Loop: HAPPY
    private IEnumerator HappyBodyLoop(float duration)
    {
        float t = 0f;
        while (duration < 0f || t < duration)
        {
            t += Time.deltaTime;

            // Bounce Y 
            float bounceY = Mathf.Sin(t * (2f * Mathf.PI / _happyBounceSpeed)) * _happyBounceAmt;
            SetParam(P_BODY_ANGLE_Y, bounceY);

            // Head sway Z 
            float swayZ = Mathf.Sin(t * (2f * Mathf.PI / 0.8f) + 0.5f) * 5f;
            SetParam(P_ANGLE_Z, -swayZ);

            // Body sway X 
            float swayX = Mathf.Sin(t * (2f * Mathf.PI / 0.7f) + 1f) * 2f;
            SetParam(P_BODY_ANGLE_X, swayX);

            yield return null;
        }
        yield return NeutralBodyReturn();
        _bodyMoodCoroutine = null;
    }

    // Body Loop: SAD
    private IEnumerator SadBodyLoop(float duration)
    {
        float t = 0f;
        while (duration < 0f || t < duration)
        {
            t += Time.deltaTime;

            // Heavy breathing 
            float breathY = -_sadHangAmt + Mathf.Sin(t * (2f * Mathf.PI / _sadSwaySpeed)) * 1f;
            SetParam(P_BODY_ANGLE_Y, breathY);

            // Head hang 
            float hangX = -10f + Mathf.Sin(t * (2f * Mathf.PI / 3.0f)) * 1f;
            SetParam(P_ANGLE_X, hangX);

            // Very subtle body sway
            float swayX = Mathf.Sin(t * (2f * Mathf.PI / 4.0f)) * 1f;
            SetParam(P_BODY_ANGLE_X, swayX);

            yield return null;
        }
        yield return NeutralBodyReturn();
        _bodyMoodCoroutine = null;
    }

    // Body Loop: SURPRISED
    private IEnumerator SurprisedBodyLoop(float duration)
    {
        float t = 0f;
        float phase1Duration = 1.0f;

        // initial shock (1 second, always plays in full)
        while (t < phase1Duration)
        {
            t += Time.deltaTime;
            float shake = Mathf.Sin(t * (2f * Mathf.PI / 0.15f)) * 0.8f;
            SetParam(P_BODY_ANGLE_X, shake);
            SetParam(P_BODY_ANGLE_Y, 8f - (t / phase1Duration) * 4f);
            yield return null;
        }

        // settle with small residual tremor (until total duration)
        while (duration < 0f || t < duration)
        {
            t += Time.deltaTime;
            float smallShake = Mathf.Sin(t * (2f * Mathf.PI / 0.4f)) * 0.5f;
            SetParam(P_BODY_ANGLE_X, smallShake);
            SetParam(P_BODY_ANGLE_Y, 4f + Mathf.Sin(t * 1f) * 0.5f);
            yield return null;
        }
        yield return NeutralBodyReturn();
        _bodyMoodCoroutine = null;
    }

    // Body Loop: THINKING
    private IEnumerator ThinkingBodyLoop(float duration)
    {
        float t = 0f;
        float nextNodTime = Random.Range(3f, 6f);

        while (duration < 0f || t < duration)
        {
            t += Time.deltaTime;

            // Gentle lean left
            float leanX = -4f + Mathf.Sin(t * (2f * Mathf.PI / _thinkingLeanSpeed)) * 1f;
            SetParam(P_BODY_ANGLE_X, leanX);

            // Slow contemplative head tilt
            float tiltX = -10f + Mathf.Sin(t * (2f * Mathf.PI / 5.0f)) * 2f;
            SetParam(P_ANGLE_X, tiltX);

            // Occasional nod
            if (t >= nextNodTime)
            {
                StartCoroutine(QuickNod());
                nextNodTime = t + Random.Range(3f, 6f);
            }

            yield return null;
        }
        yield return NeutralBodyReturn();
        _bodyMoodCoroutine = null;
    }

    private IEnumerator QuickNod()
    {
        float elapsed     = 0f;
        float nodDuration = 0.4f;
        while (elapsed < nodDuration)
        {
            elapsed += Time.deltaTime;
            float nod = Mathf.Sin(elapsed / nodDuration * Mathf.PI) * -5f;
            var p = _cubismModel.Parameters.FindById(P_ANGLE_X);
            if (p != null) p.Value += nod;
            yield return null;
        }
    }

    // Body Loop: SHY
    private IEnumerator ShyBodyLoop(float duration)
    {
        float t = 0f;
        while (duration < 0f || t < duration)
        {
            t += Time.deltaTime;

            // Head tilt — big, bashful sway
            float tiltZ = _shyTiltAmt + Mathf.Sin(t * (2f * Mathf.PI / _shyWiggleSpeed)) * 4f;
            SetParam(P_ANGLE_Z, tiltZ);

            // Body wriggle X
            float wiggleX = -5f + Mathf.Sin(t * (2f * Mathf.PI / 2.0f)) * 2f;
            SetParam(P_BODY_ANGLE_X, wiggleX);

            // Body wriggle Y
            float wiggleY = -3f + Mathf.Sin(t * (2f * Mathf.PI / 1.8f) + 1f) * 1f;
            SetParam(P_BODY_ANGLE_Y, wiggleY);

            yield return null;
        }
        yield return NeutralBodyReturn();
        _bodyMoodCoroutine = null;
    }

    // Body Return: NEUTRAL
    private IEnumerator NeutralBodyReturn()
    {
        float duration = 1.5f;
        float elapsed  = 0f;

        string[] bodyParams = {
            P_BODY_ANGLE_X, P_BODY_ANGLE_Y, P_BODY_ANGLE_Z,
            P_ANGLE_X, P_ANGLE_Y, P_ANGLE_Z
        };

        var startVals = bodyParams.ToDictionary(p => p, p => GetParam(p));

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            foreach (var p in bodyParams)
                SetParam(p, Mathf.Lerp(startVals[p], 0f, t));
            yield return null;
        }

        foreach (var p in bodyParams)
            SetParam(p, 0f);

        _idleSwayMultiplier = 1.0f;
        _bodyMoodCoroutine  = null;
    }

    // Debug Tools
    [ContextMenu("Debug: Print Parameter Ranges")]
    private void DebugParameterRanges()
    {
        if (_cubismModel == null)
        {
            Debug.LogWarning("[AiraController] CubismModel not assigned — cannot print parameter ranges.");
            return;
        }
        var sb = new StringBuilder();
        sb.AppendLine($"[PARAM RANGES] Model has {_cubismModel.Parameters.Length} parameters:\n");
        foreach (var param in _cubismModel.Parameters)
        {
            sb.AppendLine($"  '{param.Id}'" +
                $"  min={param.MinimumValue,7:F2}" +
                $"  max={param.MaximumValue,7:F2}" +
                $"  default={param.DefaultValue,7:F2}");
        }
        Debug.Log(sb.ToString());
    }

    // Breathing
    private void StartBreathing()
    {
        if (_breathingCoroutine != null) StopCoroutine(_breathingCoroutine);
        _breathingCoroutine = StartCoroutine(BreathingCoroutine());
    }

    private IEnumerator BreathingCoroutine()
    {
        float time = 0f;
        while (true)
        {
            time += Time.deltaTime;
            float raw = (Mathf.Sin((2f * Mathf.PI / _breathPeriodSeconds) * time) + 1f) * 0.5f;
            SetParam(P_BREATH, raw * _idleSwayMultiplier);
            yield return null;
        }
    }

    // Eye-Blink
    private void EnsureEyeBlinkActive()
    {
        if (_eyeBlinkController != null)
        {
            _eyeBlinkController.enabled = true;
            Debug.Log("[AiraController] CubismEyeBlinkController active.");
        }
        else
        {
            Debug.LogWarning("[AiraController] CubismEyeBlinkController not assigned — auto-blink disabled.");
        }
    }

    // Parameter Helpers
    private void SetParam(string parameterId, float value)
    {
        if (_cubismModel == null) return;
        var param = _cubismModel.Parameters.FindById(parameterId);
        if (param == null)
        {
            Debug.LogWarning($"[AiraController] Parameter not found: \"{parameterId}\"");
            return;
        }
        param.Value = value;
    }

    private float GetParam(string parameterId)
    {
        if (_cubismModel == null) return 0f;
        var param = _cubismModel.Parameters.FindById(parameterId);
        return param != null ? param.Value : 0f;
    }

    // Validation
    private void ValidateComponents()
    {
        if (_cubismModel          == null) Debug.LogError("[AiraController] CubismModel is not assigned!");
        if (_eyeBlinkController   == null) Debug.LogWarning("[AiraController] CubismEyeBlinkController is not assigned.");
        if (_mouthController      == null) Debug.LogWarning("[AiraController] CubismMouthController is not assigned.");
        if (_expressionController == null) Debug.LogWarning("[AiraController] CubismExpressionController is not assigned — expression fallback will use raw parameters.");
    }
}
