using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexCoordinates 
{
    private int x, z;
	public int X
	{
		get
		{
			return x;
		}
	}

	public int Z
	{
		get
		{
			return z;
		}
	}

	public int Y
	{
		get
		{
			return -X - Z;
		}
	}

	public HexCoordinates(int x, int z)
	{
		this.x = x;
		this.z = z;
	}
	public static HexCoordinates FromOffsetCoordinates(int x, int z)
	{
		return new HexCoordinates(x, z);
	}




}
