using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DungeonPainter.Core
{
    /// <summary>
    /// Helper class for creating materials that work with URP, HDRP, and Built-in
    /// Automatically detects the render pipeline and uses the correct shader
    /// </summary>
    public static class MaterialHelper
    {
        public enum RenderPipeline
        {
            BuiltIn,
            URP,
            HDRP
        }

        private static RenderPipeline? cachedPipeline = null;

        /// <summary>
        /// Detects which render pipeline is currently active
        /// </summary>
        public static RenderPipeline GetCurrentPipeline()
        {
            if (cachedPipeline.HasValue)
                return cachedPipeline.Value;

#if UNITY_EDITOR
            var currentPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            
            if (currentPipeline == null)
            {
                cachedPipeline = RenderPipeline.BuiltIn;
            }
            else
            {
                string pipelineName = currentPipeline.GetType().Name;
                
                if (pipelineName.Contains("Universal") || pipelineName.Contains("URP"))
                {
                    cachedPipeline = RenderPipeline.URP;
                }
                else if (pipelineName.Contains("HD") || pipelineName.Contains("HDRP"))
                {
                    cachedPipeline = RenderPipeline.HDRP;
                }
                else
                {
                    cachedPipeline = RenderPipeline.BuiltIn;
                }
            }
#else
            cachedPipeline = RenderPipeline.BuiltIn;
#endif

            Debug.Log($"Detected Render Pipeline: {cachedPipeline.Value}");
            return cachedPipeline.Value;
        }

        /// <summary>
        /// Creates a material with the correct shader for the current render pipeline
        /// </summary>
        public static Material CreateMaterial(Color color, bool isTransparent = false)
        {
            RenderPipeline pipeline = GetCurrentPipeline();
            Material mat = null;
            Shader shader = null;

            switch (pipeline)
            {
                case RenderPipeline.URP:
                    shader = Shader.Find(isTransparent ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Lit");
                    if (shader != null)
                    {
                        mat = new Material(shader);
                        mat.SetColor("_BaseColor", color);
                        
                        if (isTransparent)
                        {
                            // Enable transparency for URP
                            mat.SetFloat("_Surface", 1); // 0 = Opaque, 1 = Transparent
                            mat.SetFloat("_Blend", 0); // 0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply
                            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            mat.SetFloat("_ZWrite", 0);
                            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        }
                    }
                    break;

                case RenderPipeline.HDRP:
                    shader = Shader.Find("HDRP/Lit");
                    if (shader != null)
                    {
                        mat = new Material(shader);
                        mat.SetColor("_BaseColor", color);
                        
                        if (isTransparent)
                        {
                            mat.SetFloat("_SurfaceType", 1);
                            mat.SetFloat("_BlendMode", 0);
                            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            mat.SetFloat("_ZWrite", 0);
                            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        }
                    }
                    break;

                case RenderPipeline.BuiltIn:
                default:
                    shader = Shader.Find("Standard");
                    if (shader != null)
                    {
                        mat = new Material(shader);
                        mat.color = color;
                        
                        if (isTransparent)
                        {
                            mat.SetFloat("_Mode", 3); // Transparent mode
                            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            mat.SetInt("_ZWrite", 0);
                            mat.DisableKeyword("_ALPHATEST_ON");
                            mat.EnableKeyword("_ALPHABLEND_ON");
                            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        }
                    }
                    break;
            }

            if (mat == null)
            {
                Debug.LogWarning($"Failed to create material for pipeline {pipeline}, falling back to default");
                mat = new Material(Shader.Find("Standard"));
                mat.color = color;
            }

            return mat;
        }

        /// <summary>
        /// Creates a material with specific properties (metallic, smoothness, etc.)
        /// </summary>
        public static Material CreateAdvancedMaterial(Color color, float metallic = 0f, float smoothness = 0.5f)
        {
            Material mat = CreateMaterial(color);
            RenderPipeline pipeline = GetCurrentPipeline();

            switch (pipeline)
            {
                case RenderPipeline.URP:
                    mat.SetFloat("_Metallic", metallic);
                    mat.SetFloat("_Smoothness", smoothness);
                    break;

                case RenderPipeline.HDRP:
                    mat.SetFloat("_Metallic", metallic);
                    mat.SetFloat("_Smoothness", smoothness);
                    break;

                case RenderPipeline.BuiltIn:
                    mat.SetFloat("_Metallic", metallic);
                    mat.SetFloat("_Glossiness", smoothness);
                    break;
            }

            return mat;
        }

        /// <summary>
        /// Clears the cached pipeline detection (useful when switching pipelines in editor)
        /// </summary>
        public static void ClearCache()
        {
            cachedPipeline = null;
        }
    }
}
