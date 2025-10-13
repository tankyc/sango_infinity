using System.Text;
using UnityEngine.UI;

namespace Sango.Game.Render.UI
{
    public class UITools
    {
        // 上升A 下降B
        // 钱C 矛D 兵E 戟F 弩G 马H 冲车I 井阑J 投石机K 木兽L 船M 投石船N 气力O 耐久P 治安Q
        // 0   1   2   3   4   5   6     7      8      9     10    11    12    13    14
        public static void ShowDamage(AnimationText aniText, int damage, int damageType = 0)
        {
            bool isUpZero = damage > 0;
            StringBuilder stringBuilder = new StringBuilder(isUpZero ? "A" : "B");
            stringBuilder.Append((char)(67 + damageType));
            UnityEngine.Color c = isUpZero ? UnityEngine.Color.yellow : UnityEngine.Color.red;
            aniText.Create(damage.ToString(), stringBuilder.ToString(), c, 2);
        }
    }
}
