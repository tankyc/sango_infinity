using Sango.Core;
using UnityEngine;

namespace Sango.Render.Model
{
    public class TroopModel : MonoBehaviour
    {
        public FlagRender flag;
        public TroopsRender troopsRender;
        public Animation animation;
        public ParticleSystem smoke;

        public Renderer[] flashRenderers;
        public bool flashFalg = false;
        Vector2 flashFactor = new Vector2(1f, 1.8f);
        float flashTime = 0.6f;
        string propertyName = "_FlashFactor";
        float curFactor = 0;
        float factorDir = 1;

        private void Awake()
        {
            if (animation == null)
                animation = GetComponent<Animation>();

            UnityTools.SetLayer(flag.gameObject, LayerMask.NameToLayer("Flag"));
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                Renderer renderer = flashRenderers[i];
                renderer.material.SetFloat(propertyName, 1);
            }

            if(troopsRender != null)
            {
                for(int i = 0; i < troopsRender.aniMaterials.Length;i++)
                {
                    troopsRender.aniMaterials[i].SetFloat(propertyName, 1);
                }
            }
        }

        public virtual void SetFlash(bool b)
        {
            flashFalg = b;
            if (!flashFalg)
            {
                for (int i = 0; i < flashRenderers.Length; i++)
                {
                    Renderer renderer = flashRenderers[i];
                    renderer.material.SetFloat(propertyName, 1);
                }

                if (troopsRender != null)
                {
                    for (int i = 0; i < troopsRender.aniMaterials.Length; i++)
                    {
                        troopsRender.aniMaterials[i].SetFloat(propertyName, 1);
                    }
                }
            }
        }

        private void Update()
        {
            if (flashFalg)
            {
                curFactor += Time.deltaTime * factorDir;
                if (curFactor > flashTime && factorDir > 0)
                {
                    curFactor = flashTime;
                    factorDir = -1;
                }
                else if (curFactor < 0 && factorDir < 0)
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

                if (troopsRender != null)
                {
                    for (int i = 0; i < troopsRender.aniMaterials.Length; i++)
                    {
                        troopsRender.aniMaterials[i].SetFloat(propertyName, v);
                    }
                }
            }
        }


        public void Init(Troop troop)
        {
            if (flag != null)
                flag.Init(troop);
            SetAniShow(0);
            UpdateTroop(troop);
            if (troopsRender != null)
            {
                troopsRender.ResetPoisitonCheck();
                troopsRender.UpdateHeight();
            }
        }

        public void UpdateTroop(Troop troop)
        {
            if (troopsRender != null)
            {
                float p = Mathf.Clamp01((float)troop.troops / 20000f);
                p = (1f-p) * (1f-p);
                p = 1f - p;
                troopsRender.SetShowPercent(p);
            }
        }

        /// <summary>
        /// 刷新旗帜所属势力:部队换旗(登庸主将 / 招降)后立刻把旗色调成新势力色。
        /// 内部带"值没变就不写"的守卫,可以随表现层刷新被高频调用。
        /// </summary>
        /// <param name="troop">所属部队</param>
        public void UpdateFlag(Troop troop)
        {
            if (flag != null)
                flag.UpdateForce(troop);
        }

        public void SetAniShow(int name, bool onlyRenderAni = false)
        {
            if (troopsRender != null)
            {
                troopsRender.SetAniType(name);

            }
            if (name == 1 && animation != null)
                animation.Play("troop_atk_1");
        }

        public void SetSmokeShow(bool b)
        {
            if (troopsRender != null)
            {
                troopsRender.SetSmokeShow(b);
            }
        }
    }
}
