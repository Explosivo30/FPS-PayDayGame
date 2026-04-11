using UnityEngine;
using UnityEngine.Rendering;

[VolumeComponentMenu("Torbellino/Anime Tonemapper")]
public class AnimeTonemapper : VolumeComponent, IPostProcessComponent
{
    public ClampedFloatParameter exposure      = new ClampedFloatParameter(1f, 0f, 4f);
    public ClampedFloatParameter contrast      = new ClampedFloatParameter(1.1f, 0.5f, 3f);
    public ClampedFloatParameter saturation    = new ClampedFloatParameter(1.3f, 0f, 3f);
    public ClampedFloatParameter shadowTint    = new ClampedFloatParameter(0.1f, 0f, 1f);
    public ClampedFloatParameter highlightSoft = new ClampedFloatParameter(0.95f, 0.5f, 1f);
    public ColorParameter shadowColor          = new ColorParameter(new Color(0.05f, 0.07f, 0.15f));

    public bool IsActive() => active;
    public bool IsTileCompatible() => false;
}
