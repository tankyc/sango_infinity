using UnityEngine;

//挂上这个脚本来预览公共界面
public class UIPreview : MonoBehaviour
{

#if UNITY_EDITOR
    public Texture2D PreviewThumbnail;
    public Texture2D PreviewImage;
#endif
}