using UnityEngine;
using UnityEngine.UI;

namespace Unity.AR.Companion.Core
{
    /// <summary>
    /// Common utility methods for UI
    /// </summary>
    public static class UIUtils
    {
        /// <summary>
        /// Force ContentSizeFitters on this object and its parent to update
        /// </summary>
        /// <param name="gameObject"></param>
        public static void UpdateConstrainedTextLayout(GameObject gameObject)
        {
            var contentSizeFitter = gameObject.GetComponent<ContentSizeFitter>();
            if (contentSizeFitter != null)
            {
                contentSizeFitter.SetLayoutHorizontal();
                contentSizeFitter.SetLayoutVertical();
            }

            contentSizeFitter = gameObject.GetComponentInParent<ContentSizeFitter>();
            if (contentSizeFitter != null)
            {
                contentSizeFitter.SetLayoutHorizontal();
                contentSizeFitter.SetLayoutVertical();
            }
        }

        internal static void SetAndCenterTexture(RawImage image, Texture texture)
        {
            image.texture = texture;
            if (texture == null)
                return;

            var width = (float) texture.width;
            var height = (float) texture.height;
            if (width > height)
            {
                var ratio = height / width;
                var offset = (1 - ratio) * 0.5f;
                image.uvRect = new Rect(offset, 0, ratio, 1);
            }
            else
            {
                var ratio = width / height;
                var offset = (1 - ratio) * 0.5f;
                image.uvRect = new Rect(0, offset, 1, ratio);
            }
        }
    }
}
