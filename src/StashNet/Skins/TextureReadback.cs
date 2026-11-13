using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// Reads a texture's pixels back from the GPU (game textures aren't CPU-readable) as RGBA bytes, row 0 =
    /// bottom. Used by the repaint and by sn skin dump.
    /// </summary>
    internal static class TextureReadback
    {
        public static byte[] ToRgba(Texture tex, int slice = -1)
        {
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            try
            {
                if (slice >= 0) Graphics.Blit(tex, rt, slice, 0);
                else Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                copy.Apply();
                return copy.GetRawTextureData();
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(copy);
            }
        }

        /// <summary>A new mipmapped texture from RGBA bytes (row 0 = bottom).</summary>
        public static Texture2D FromRgba(byte[] rgba, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name };
            tex.SetPixelData(rgba, 0);
            tex.Apply(true, true);
            return tex;
        }

        public static byte[] ToPng(byte[] rgba, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixelData(rgba, 0);
                tex.Apply();
                return ImageConversion.EncodeToPNG(tex);
            }
            finally
            {
                Object.Destroy(tex);
            }
        }
    }
}
