using System.Text;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UITools
    {

        // 上升A 下降B
        // 钱C 矛D 兵E 戟F 弩G 粮H 马I 冲车J 井阑K 投石机L 木兽M 船N 投石船O 气力P 耐久Q 治安R
        // 0   1   2   3   4   5   6     7      8      9     10    11    12    13    14  15
        public static void ShowInfo(AnimationText aniText, int damage, int damageType = 0)
        {
            bool isUpZero = damage > 0;
            StringBuilder stringBuilder = new StringBuilder(isUpZero ? "A" : "B");
            stringBuilder.Append((char)(67 + damageType));
            UnityEngine.Color c = isUpZero ? new UnityEngine.Color(0.988f, 0.929f, 0.261f) : new UnityEngine.Color(1f, 0.479f, 0.231f);
            aniText.flipY = isUpZero;
            aniText.Create(damage.ToString(), stringBuilder.ToString(), c, 2);
        }

        public static void ShowInfoNoColor(AnimationText aniText, int damage, int damageType = 0)
        {
            bool isUpZero = damage > 0;
            StringBuilder stringBuilder = new StringBuilder(isUpZero ? "A" : "B");
            stringBuilder.Append((char)(67 + damageType));
            aniText.flipY = isUpZero;
            aniText.Create(damage.ToString(), stringBuilder.ToString(), 2.1f);
        }
    }
}
