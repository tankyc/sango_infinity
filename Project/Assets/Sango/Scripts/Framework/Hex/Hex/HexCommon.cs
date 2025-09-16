using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum HEX_LAYOUT
{
    ODD = -1, // ����������
    EVENT = 1, // ż��������
}

public enum HEX_STYLE
{
    HORIZONTAL = 1, // ˮƽ����
    VERTICAL = 2,// ��ֱ����
}
public class HexCommon 
{
    /// <summary>
    /// Ĭ�ϲ���
    /// </summary>
    public static HEX_LAYOUT HEX_LAYOUT = HEX_LAYOUT.EVENT;

    /// <summary>
    /// Ĭ�Ϸ��
    /// </summary>
    public static HEX_STYLE HEX_STYLE = HEX_STYLE.VERTICAL;

	/// <summary>
	///��뾶
	/// </summary>
	public static float m_OuterRadius = 5;

	public static float m_InnerRadius { get { return m_OuterRadius * Mathf.Cos(Mathf.PI / 180 * 30); } }

	public static Vector3 HGetCorners(int i)
	{
		if (i == 0)
			return new Vector3(0f, 0f, m_OuterRadius);
		else if (i == 1)
			return new Vector3(m_InnerRadius, 0f, 0.5f * m_OuterRadius);
		else if (i == 2)
			return new Vector3(m_InnerRadius, 0f, -0.5f * m_OuterRadius);
		else if (i == 3)
			return new Vector3(0f, 0f, -m_OuterRadius);
		else if (i == 4)
			return new Vector3(-m_InnerRadius, 0f, -0.5f * m_OuterRadius);
		else if (i == 5)
			return new Vector3(-m_InnerRadius, 0f, 0.5f * m_OuterRadius);
		else if (i == 6)
			return new Vector3(0f, 0f, m_OuterRadius);
		return new Vector3(0, 0, 0);
	}


	public static Vector3 VGetCorners(int i)
	{
		if (i == 0)
			return new Vector3(0.5f * m_OuterRadius, 0f, m_InnerRadius);
		else if (i == 1)
			return new Vector3(m_OuterRadius, 0f, 0);
		else if (i == 2)
			return new Vector3(0.5f * m_OuterRadius, 0f, -m_InnerRadius);
		else if (i == 3)
			return new Vector3(-0.5f * m_OuterRadius, 0f, -m_InnerRadius);
		else if (i == 4)
			return new Vector3(-m_OuterRadius, 0f, 0);
		else if (i == 5)
			return new Vector3(-0.5f * m_OuterRadius, 0f, m_InnerRadius);
		else if (i == 6)
			return new Vector3(0.5f * m_OuterRadius, 0f, m_InnerRadius);
		return new Vector3(0, 0, 0);
	}

}


