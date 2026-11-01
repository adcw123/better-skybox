using SkyboxVR;
using UnityEngine;
namespace SkyboxUsability
{
    // These are exclusively the Sprite/Texture2D objects created by the original
    // FillVideoInfoToItem/FillPictureInfoToItem methods, never shared UI assets.
    // Pooled cards retain one thumbnail; rebinding releases their previous one.
    public sealed class ThumbnailLifetime:MonoBehaviour
    {
        Sprite ownedSprite;
        Texture2D ownedTexture;
        FileItemEvent cell;
        void Awake(){cell=GetComponent<FileItemEvent>();}
        public void Track()
        {
            if(!(cell.FileInfo is MediaFileInfo)||cell.ThumbnailImage==null)return;
            var sprite=cell.ThumbnailImage.sprite;
            if(sprite==null||sprite==ownedSprite)return;
            Release();ownedSprite=sprite;ownedTexture=sprite.texture;
        }
        public void Release()
        {
            if(cell!=null&&cell.ThumbnailImage!=null&&cell.ThumbnailImage.sprite==ownedSprite)cell.ThumbnailImage.sprite=null;
            if(ownedSprite!=null)Destroy(ownedSprite);
            if(ownedTexture!=null)Destroy(ownedTexture);
            ownedSprite=null;ownedTexture=null;
        }
        void OnDestroy(){Release();}
    }
}
