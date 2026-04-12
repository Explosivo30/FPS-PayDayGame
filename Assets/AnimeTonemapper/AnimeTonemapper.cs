using UnityEngine;
using UnityEngine.Rendering;

[VolumeComponentMenu("Torbellino/Anime Tonemapper")]
public class AnimeTonemapper : VolumeComponent, IPostProcessComponent
{
    [Header("Base")]
    public ClampedFloatParameter exposure        = new ClampedFloatParameter(1.0f,  0f,   4f);
    public ClampedFloatParameter contrast        = new ClampedFloatParameter(1.15f, 0.5f, 3f);

    [Header("Color")]
    public ClampedFloatParameter saturation      = new ClampedFloatParameter(1.5f,  0f,   4f);
    public ClampedFloatParameter neonBoost       = new ClampedFloatParameter(1.4f,  1f,   4f);   // boost exponencial en colores ya saturados

    [Header("Shadows")]
    public ClampedFloatParameter shadowHueShift  = new ClampedFloatParameter(0.72f, 0f,   1f);   // hue target en sombras (0.72 = azul/violeta)
    public ClampedFloatParameter shadowHueStr    = new ClampedFloatParameter(0.55f, 0f,   1f);   // fuerza del hue shift
    public ClampedFloatParameter shadowThreshold = new ClampedFloatParameter(0.35f, 0f,   1f);   // hasta donde llegan las sombras
    public ColorParameter        shadowColor     = new ColorParameter(new Color(0.04f, 0.03f, 0.12f)); // color base de sombras

    [Header("Highlights")]
    public ClampedFloatParameter highlightSoft     = new ClampedFloatParameter(0.9f,  0.5f, 1f);
    public ClampedFloatParameter highlightHueShift = new ClampedFloatParameter(0.08f, 0f,   1f);  // hue target highlights (0.08 = amarillo/naranja)
    public ClampedFloatParameter highlightHueStr   = new ClampedFloatParameter(0.3f,  0f,   1f);  // fuerza del hue shift en highlights
    public ClampedFloatParameter highlightThreshold = new ClampedFloatParameter(0.72f, 0f,   1f); // desde donde empiezan los highlights

    [Header("Cel Banding")]
    public BoolParameter         celBanding       = new BoolParameter(true);
    public ClampedFloatParameter celBands         = new ClampedFloatParameter(3f,    2f,   6f);   // numero de bandas
    public ClampedFloatParameter celHardness      = new ClampedFloatParameter(0.85f, 0f,   1f);   // dureza del corte (1 = duro, 0 = suave)

    public bool IsActive() => active;
    public bool IsTileCompatible() => false;
}
