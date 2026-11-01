using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Torbellino.Rendering
{
    public enum RisoPaletteSize { Three = 3, Four = 4 }

    [Serializable]
    public sealed class RisoPaletteParameter : VolumeParameter<RisoPaletteSize>
    {
        public RisoPaletteParameter(RisoPaletteSize value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [Serializable, VolumeComponentMenu("Torbellino/Risograph")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class RisographVolume : VolumeComponent, IPostProcessComponent
    {
        [Header("Mezcla y contraste")]
        [Tooltip("0 desactiva el efecto; 1 aplica la impresión completa.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Exposición adicional en pasos EV, después del tonemapping de la escena.")]
        public ClampedFloatParameter exposure = new ClampedFloatParameter(0.2f, -3f, 3f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(1.08f, 0.5f, 2.5f);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(1.3f, 0f, 2f);
        [Tooltip("Abre los tonos medios para que no se acumulen en la tinta azul oscura.")]
        public ClampedFloatParameter midtoneLift = new ClampedFloatParameter(0.65f, 0f, 1f);
        [Tooltip("Reserva el papel crema para las zonas iluminadas, incluso con colores saturados.")]
        public ClampedFloatParameter paperHighlights = new ClampedFloatParameter(0.6f, 0f, 1f);

        [Header("Paleta (el papel cuenta como un color)")]
        [Tooltip("Tres: azul oscuro, rosa y papel. Cuatro añade el azul medio.")]
        public RisoPaletteParameter paletteSize = new RisoPaletteParameter(RisoPaletteSize.Four);
        public ColorParameter shadowInk = new ColorParameter(new Color(0.157f, 0.216f, 0.478f));
        public ColorParameter blueInk = new ColorParameter(new Color(0.31f, 0.48f, 0.82f));
        public ColorParameter pinkInk = new ColorParameter(new Color(1f, 0.29f, 0.54f));
        public ColorParameter paper = new ColorParameter(new Color(1f, 0.918f, 0.792f));

        [Header("Semitonos de puntos")]
        public ClampedFloatParameter halftone = new ClampedFloatParameter(0.7f, 0f, 1f);
        [Tooltip("Tamaño de celda en píxeles a 1080p; mantiene la escala al cambiar resolución.")]
        public ClampedFloatParameter dotSize = new ClampedFloatParameter(4.5f, 2f, 16f);
        public ClampedFloatParameter screenAngle = new ClampedFloatParameter(35f, 0f, 90f);

        [Header("Papel e imperfecciones")]
        [Tooltip("Variación de las fibras del papel; 0 mantiene los colores exactos de la paleta.")]
        public ClampedFloatParameter paperGrain = new ClampedFloatParameter(0.085f, 0f, 0.5f);
        [Tooltip("Pequeñas zonas sin tinta que dejan ver el papel.")]
        public ClampedFloatParameter inkWear = new ClampedFloatParameter(0.018f, 0f, 0.3f);
        [Tooltip("Desplazamiento del registro de la tinta rosa, en píxeles a 1080p.")]
        public ClampedFloatParameter misregistration = new ClampedFloatParameter(0.65f, 0f, 3f);

        public bool IsActive() => active && intensity.value > 0f;
        public bool IsTileCompatible() => false;
    }
}
