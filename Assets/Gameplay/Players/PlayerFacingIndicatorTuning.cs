namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Serialization;

    [CreateAssetMenu(menuName = "Crescendo/Players/Facing Indicator Tuning")]
    public sealed class PlayerFacingIndicatorTuning : ScriptableObject
    {
        [Header("Ground placement")]
        [Min(0.01f)] public float radius = 0.35f;
        [Min(0.01f)] public float manualAimRadius = 0.85f;
        [Min(0.001f)] public float groundHeight = 0.025f;

        [Header("Movement triangle")]
        [ColorUsage(false)] public Color movementColor = Color.white;
        [InspectorName("Multiply With Player Color")] public bool movementMultiplyWithPlayerColor;
        [Min(0.01f)] public float movingRadius = 0.65f;
        [Range(1f, 120f)] public float movementAngle = 65f;
        [Min(0.01f)] public float movementLength = 0.25f;
        [Range(0f, 0.95f)] public float movementOpacity = 0.35f;
        [Range(0f, 0.95f)] public float stationaryOpacity = 0.15f;

        [Header("Aim triangle")]
        [ColorUsage(false)] public Color aimColor = Color.white;
        [InspectorName("Multiply With Player Color")] public bool aimMultiplyWithPlayerColor = true;
        [Range(1f, 120f)] public float aimAngle = 30f;
        [Min(0.01f)] public float aimLength = 0.17f;
        [FormerlySerializedAs("aimOpacity")]
        [Range(0f, 0.95f)] public float autoAimOpacity = 0.65f;
        [Range(0f, 0.95f)] public float manualAimOpacity = 0.65f;

        [Header("Transitions")]
        [Min(0f)] public float transitionDuration = 0.12f;
    }
}
