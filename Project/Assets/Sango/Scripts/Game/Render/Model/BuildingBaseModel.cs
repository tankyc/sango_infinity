using UnityEngine;
using Sango.Core;

namespace Sango.Render.Model
{
    public class BuildingBaseModel : MonoBehaviour
    {
        public FlagRender[] flags;

        public Renderer[] flashRenderers;
        public bool flashFalg = false;
        Vector2 flashFactor = new Vector2(1f, 1.8f);
        float flashTime = 0.6f;
        string propertyName = "_FlashFactor";
        float curFactor = 0;
        float factorDir = 1;

        public GameObject maxLevelEffect;

        protected virtual void Awake()
        {
            for(int i = 0; i < flags.Length; i++)
            {
                UnityTools.SetLayer(flags[i].gameObject, LayerMask.NameToLayer("Flag"));
            }

            flashRenderers = GetComponentsInChildren<MeshRenderer>();
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                Renderer renderer = flashRenderers[i];
                renderer.material.SetFloat(propertyName, 1);
            }
        }

        public virtual void SetFlash(bool b)
        {
            flashFalg = b;
            if(!flashFalg)
            {
                for (int i = 0; i < flashRenderers.Length; i++)
                {
                    Renderer renderer = flashRenderers[i];
                    renderer.material.SetFloat(propertyName, 1);
                }
            }
        }

        private void Update()
        {
            if(flashFalg)
            {
                curFactor += Time.deltaTime * factorDir;
                if(curFactor > flashTime && factorDir > 0)
                {
                    curFactor = flashTime;
                    factorDir = -1;
                }
                else if(curFactor < 0 && factorDir < 0)
                {
                    curFactor = 0;
                    factorDir = 1;
                }
                float v = Mathf.Lerp(flashFactor.x, flashFactor.y, curFactor / flashTime);

                for (int i = 0; i < flashRenderers.Length; i++)
                {
                    Renderer renderer = flashRenderers[i];
                    renderer.material.SetFloat(propertyName, v);
                }
            }
        }

        public void Init(BuildingBase building)
        {
            if (building.mForce == null)
            {
                foreach (FlagRender flag in flags)
                {
                    if (flag != null)
                    {
                        flag.gameObject.SetActive(false);
                    }
                }
                return;
            }

            if (flags != null)
            {
                foreach (FlagRender flag in flags)
                {
                    if (flag != null)
                    {
                        flag.gameObject.SetActive(true);
                        flag.Init(building.mForce);
                    }
                }
            }
        }
    }
}
