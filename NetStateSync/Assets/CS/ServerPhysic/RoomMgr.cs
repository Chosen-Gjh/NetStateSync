using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomMgr : MonoBehaviour
{
    public static RoomMgr Instance;
    public GameObject AvatarGo;
    public Dictionary<string, SyncActor> ActorDic;
    private void Awake()
    {
        Instance = this;
        ActorDic = new Dictionary<string, SyncActor>();
    }
    
    public void OnEnter(string msgArgs)
	{
		Debug.Log("OnEnter " + msgArgs);
		//解析参数
		string[] split = msgArgs.Split(',');
		string desc = split[0];
		float x = float.Parse(split[1]);
		float y = float.Parse(split[2]);
		float z = float.Parse(split[3]);
		float eulY = float.Parse(split[4]);
		//添加一个角色
		GameObject obj = (GameObject)Instantiate(AvatarGo);
		obj.transform.position = new Vector3(x, y, z);
		obj.transform.eulerAngles = new Vector3(0, eulY, 0);
		SyncActor h = obj.AddComponent<SyncActor>();
		h.desc = desc;
		ActorDic.Add(desc, h);
	}

    public void OnList(string msgArgs)
	{
		Debug.Log("OnList " + msgArgs);
		//解析参数
		string[] split = msgArgs.Split(',');
		int count = (split.Length - 1) / 6;
		for (int i = 0; i < count; i++)
		{
			string desc = split[i * 6 + 0];
			float x = float.Parse(split[i * 6 + 1]);
			float y = float.Parse(split[i * 6 + 2]);
			float z = float.Parse(split[i * 6 + 3]);
			float eulY = float.Parse(split[i * 6 + 4]);
			int hp = int.Parse(split[i * 6 + 5]);
			//添加一个角色
			GameObject obj = (GameObject)Instantiate(AvatarGo);
			obj.transform.position = new Vector3(x, y, z);
			obj.transform.eulerAngles = new Vector3(0, eulY, 0);
			SyncActor h = obj.AddComponent<SyncActor>();
			h.desc = desc;
			ActorDic.Add(desc, h);
		}
	}

    public void OnMove(string msgArgs)
	{
		Debug.Log("OnMove " + msgArgs);
		//解析参数
		string[] split = msgArgs.Split(',');
		string desc = split[0];
		float x = float.Parse(split[1]);
		float y = float.Parse(split[2]);
		float z = float.Parse(split[3]);
		//移动
		if (!ActorDic.ContainsKey(desc))
			return;
		SyncActor h = ActorDic[desc];
		Vector3 targetPos = new Vector3(x, y, z);
		h.MoveTo(targetPos);

	}

    public void OnLeave(string msgArgs)
	{
		Debug.Log("OnLeave " + msgArgs);
		//解析参数
		string[] split = msgArgs.Split(',');
		string desc = split[0];
		//删除
		if (!ActorDic.ContainsKey(desc))
			return;
		SyncActor h = ActorDic[desc];
		Destroy(h.gameObject);
		ActorDic.Remove(desc);
	}

    public void OnAttack(string msgArgs)
	{
		Debug.Log("OnAttack " + msgArgs);
		//解析参数
		string[] split = msgArgs.Split(',');
		string desc = split[0];
		float eulY = float.Parse(split[1]);
		//攻击动作
		if (!ActorDic.ContainsKey(desc))
			return;
		SyncActor h = ActorDic[desc];
		h.SyncAttack(eulY);
	}

    public void OnDie(string msgArgs)
	{
		Debug.Log("OnAttack " + msgArgs);
		//解析参数
		string[] split = msgArgs.Split(',');
		string attDesc = split[0];
		string hitDesc = split[0];
		//死了
		if (!ActorDic.ContainsKey(hitDesc))
			return;
		SyncActor h = ActorDic[hitDesc];
		h.gameObject.SetActive(false);

	}
}
