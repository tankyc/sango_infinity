/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using UnityEngine;
namespace Sango
{
    public interface IAssetReleaser
    {
        void Clear();
        void Set(int index);
    }

    public class AssetReleaser : MonoBehaviour, IAssetReleaser
    {
        [System.NonSerialized]
        public int abIndex = -1;
        public void Set(int index)
        {
            if (abIndex >= 0)
            {
                AssetBundleManager.Unload(abIndex);
            }
            abIndex = index;
        }
        void OnDestroy()
        {
            Clear();
        }

        public void Clear()
        {
            if (abIndex != -1)
            {
                AssetBundleManager.Unload(abIndex);
                abIndex = -1;
            }
        }

        /// <summary>
        /// 自动施放资源,资源随着GO的销毁而释放
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="node"></param>
        public static void AutoRelease(int abIndex, GameObject node)
        {
            if (abIndex < 0 || node == null)
                return;

            IAssetReleaser ara = node.GetComponent<IAssetReleaser>();
            if (ara == null)
                ara = node.AddComponent<AssetReleaser>();

            ara.Set(abIndex);
        }
        public static void AutoRelease(string fileName, GameObject node)
        {
            AutoRelease(AssetBundleManager.NameToIndex(fileName), node);
        }
    }

}