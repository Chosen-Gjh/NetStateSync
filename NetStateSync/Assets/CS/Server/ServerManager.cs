using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

public class ClientState
{
	public Socket socket;
	public byte[] readBuff = new byte[1024];
	public int hp = -100;
	public float x = 0;
	public float y = 0;
	public float z = 0;
	public float eulY = 0;
}
public class EventHandler
{
	public static void OnDisconnect(ClientState c)
	{
		string desc = c.socket.RemoteEndPoint.ToString();
		string sendStr = "Leave|" + desc + ",";
		foreach (ClientState cs in ServerData.clients.Values)
		{
			ServerData.Send(cs, sendStr);
		}
	}
}
public class MsgHandler
{
	public static void MsgEnter(ClientState c, string msgArgs,Socket clientfd)
	{
		string[] split = msgArgs.Split(',');
		string desc = split[0];
		float x = float.Parse(split[1]);
		float y = float.Parse(split[2]);
		float z = float.Parse(split[3]);
		float eulY = float.Parse(split[4]);
		c.hp = 100;
		c.x = x;
		c.y = y;
		c.z = z;
		c.eulY = eulY;
		string sendStr = "Enter|" + msgArgs;
		RoomMgr.Instance.OnEnter(msgArgs);
		foreach (ClientState cs in ServerData.clients.Values)
		{
			ServerData.Send(cs, sendStr);
		}
	}

	public static void MsgList(ClientState c, string msgArgs,Socket clientfd)
	{
		string sendStr = "List|";
		foreach (ClientState cs in ServerData.clients.Values)
		{
			sendStr += cs.socket.RemoteEndPoint.ToString() + ",";
			sendStr += cs.x.ToString() + ",";
			sendStr += cs.y.ToString() + ",";
			sendStr += cs.z.ToString() + ",";
			sendStr += cs.eulY.ToString() + ",";
			sendStr += cs.hp.ToString() + ",";
		}
		RoomMgr.Instance.OnList(msgArgs);
		ServerData.Send(c, sendStr);
	}

	public static void MsgMove(ClientState c, string msgArgs,Socket clientfd)
	{
		string[] split = msgArgs.Split(',');
		string desc = split[0];
		float x = float.Parse(split[1]);
		float y = float.Parse(split[2]);
		float z = float.Parse(split[3]);
		c.x = x;
		c.y = y;
		c.z = z;
		string sendStr = "Move|" + msgArgs+$"|{SyncPos(clientfd,msgArgs)}";
		RoomMgr.Instance.OnMove(msgArgs);
		foreach (ClientState cs in ServerData.clients.Values)
		{
			ServerData.Send(cs, sendStr);
		}
	}

	public static void MsgAttack(ClientState c, string msgArgs,Socket clientfd)
	{
		string sendStr = "Attack|" + msgArgs;//+$"|{SyncPos(clientfd,msgArgs)}";;
		RoomMgr.Instance.OnAttack(msgArgs);
		foreach (ClientState cs in ServerData.clients.Values)
		{
			ServerData.Send(cs, sendStr);
		}
	}

	public static void MsgHit(ClientState c, string msgArgs,Socket clientfd)
	{
		string[] split = msgArgs.Split(',');
		string attDesc = split[0];
		string hitDesc = split[1];
		ClientState hitCS = null;
		foreach (ClientState cs in ServerData.clients.Values)
		{
			if (cs.socket.RemoteEndPoint.ToString() == hitDesc)
				hitCS = cs;
		}
		if (hitCS == null)
			return;

		hitCS.hp -= 25;
		if (hitCS.hp <= 0)
		{
			string sendStr = "Die|" + hitCS.socket.RemoteEndPoint.ToString();//+$"|{SyncPos(clientfd,msgArgs)}";;
			RoomMgr.Instance.OnDie(sendStr);
			foreach (ClientState cs in ServerData.clients.Values)
			{
				ServerData.Send(cs, sendStr);
			}
		}
	}

	public static string SyncPos(Socket st,string msgArgs)
	{
		if (ServerData.clients.TryGetValue(st, out var Val))
		{
			var strs = msgArgs.Split('|');
			if (strs == null || strs.Length <= 0) return null;
			if(RoomMgr.Instance.ActorDic.TryGetValue(strs[0].Split(',')[0],out var syncActor))
			{
				var vec3 = syncActor.gameObject.transform.position;
				return $"{vec3.x},{vec3.y},{vec3.z}";
			}
		}
		return null;
	}
}
class ServerData
{
	public static Socket listenfd;
	public static Dictionary<Socket, ClientState> clients = new Dictionary<Socket, ClientState>();
	public List<Socket> checkRead;
	public static void Send(ClientState cs, string sendStr)
	{
		byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);
		cs.socket.Send(sendBytes);
	}


}
public class ServerManager : MonoBehaviour
{
	private ServerData ServerData;
	// Start is called before the first frame update
	void Start()
	{
		ServerData = new ServerData();
		ServerData.checkRead = new List<Socket>();
	    //Socket
	    ServerData.listenfd = new Socket(AddressFamily.InterNetwork,
						SocketType.Stream, ProtocolType.Tcp);
		//Bind
		IPAddress ipAdr = IPAddress.Parse("127.0.0.1");
		IPEndPoint ipEp = new IPEndPoint(ipAdr, 8888);
		ServerData.listenfd.Bind(ipEp);
		//Listen
		ServerData.listenfd.Listen(0);
		//checkRead
		ServerData.checkRead = new List<Socket>();
	}

    // Update is called once per frame
    void Update()
    {
	    ServerData.checkRead.Clear();
	    ServerData.checkRead.Add(ServerData.listenfd);
		foreach (ClientState s in ServerData.clients.Values)
		{
			ServerData.checkRead.Add(s.socket);
		}
		//select
		Socket.Select(ServerData.checkRead, null, null, 1000);
		foreach (Socket s in ServerData.checkRead)
		{
			if (s == ServerData.listenfd)
			{
				ReadListenfd(s);
			}
			else
			{
				ReadClientfd(s);
			}
		}
	}
	public static void ReadListenfd(Socket listenfd)
	{
		Debug.Log("Accept");
		Socket clientfd = listenfd.Accept();
		ClientState state = new ClientState();
		state.socket = clientfd;
		ServerData.clients.Add(clientfd, state);
	}
	public static bool ReadClientfd(Socket clientfd)
	{
		ClientState state = ServerData.clients[clientfd];
		int count = 0;
		try
		{
			count = clientfd.Receive(state.readBuff);
		}
		catch (SocketException ex)
		{
			MethodInfo mei = typeof(EventHandler).GetMethod("OnDisconnect");
			object[] ob = { state };
			mei.Invoke(null, ob);

			clientfd.Close();
			ServerData.clients.Remove(clientfd);
			Debug.Log("Receive SocketException " + ex.ToString());
			return false;
		}
		if (count <= 0)
		{
			MethodInfo mei = typeof(EventHandler).GetMethod("OnDisconnect");
			object[] ob = { state };
			mei.Invoke(null, ob);

			clientfd.Close();
			ServerData.clients.Remove(clientfd);
			Debug.Log("Socket Close");
			return false;
		}
		string recvStr =
				System.Text.Encoding.Default.GetString(state.readBuff, 0, count);
		string[] split = recvStr.Split('|');
		Debug.Log("Recv " + recvStr);
		string msgName = split[0];
		string msgArgs = split[1];
		string funName = "Msg" + msgName;
		MethodInfo mi = typeof(MsgHandler).GetMethod(funName);
		object[] o = { state, msgArgs,clientfd };
		mi.Invoke(null, o);
		return true;
	}
}
