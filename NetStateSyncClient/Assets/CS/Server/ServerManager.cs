using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Linq;

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
		foreach (ClientState cs in MainClass.clients.Values)
		{
			MainClass.Send(cs, sendStr);
		}
	}
}
public class MsgHandler
{
	public static void MsgEnter(ClientState c, string msgArgs)
	{
		//解析参数
		string[] split = msgArgs.Split(',');
		string desc = split[0];
		float x = float.Parse(split[1]);
		float y = float.Parse(split[2]);
		float z = float.Parse(split[3]);
		float eulY = float.Parse(split[4]);
		//赋值
		c.hp = 100;
		c.x = x;
		c.y = y;
		c.z = z;
		c.eulY = eulY;
		//广播
		string sendStr = "Enter|" + msgArgs;
		foreach (ClientState cs in MainClass.clients.Values)
		{
			MainClass.Send(cs, sendStr);
		}
	}

	public static void MsgList(ClientState c, string msgArgs)
	{
		string sendStr = "List|";
		foreach (ClientState cs in MainClass.clients.Values)
		{
			sendStr += cs.socket.RemoteEndPoint.ToString() + ",";
			sendStr += cs.x.ToString() + ",";
			sendStr += cs.y.ToString() + ",";
			sendStr += cs.z.ToString() + ",";
			sendStr += cs.eulY.ToString() + ",";
			sendStr += cs.hp.ToString() + ",";
		}
		MainClass.Send(c, sendStr);
	}

	public static void MsgMove(ClientState c, string msgArgs)
	{
		//解析参数
		string[] split = msgArgs.Split(',');
		string desc = split[0];
		float x = float.Parse(split[1]);
		float y = float.Parse(split[2]);
		float z = float.Parse(split[3]);
		//赋值
		c.x = x;
		c.y = y;
		c.z = z;
		//广播
		string sendStr = "Move|" + msgArgs;
		foreach (ClientState cs in MainClass.clients.Values)
		{
			MainClass.Send(cs, sendStr);
		}
	}

	public static void MsgAttack(ClientState c, string msgArgs)
	{
		//广播
		string sendStr = "Attack|" + msgArgs;
		foreach (ClientState cs in MainClass.clients.Values)
		{
			MainClass.Send(cs, sendStr);
		}
	}

	public static void MsgHit(ClientState c, string msgArgs)
	{
		//解析参数
		string[] split = msgArgs.Split(',');
		string attDesc = split[0];
		string hitDesc = split[1];
		//被攻击
		ClientState hitCS = null;
		foreach (ClientState cs in MainClass.clients.Values)
		{
			if (cs.socket.RemoteEndPoint.ToString() == hitDesc)
				hitCS = cs;
		}
		if (hitCS == null)
			return;

		hitCS.hp -= 25;
		if (hitCS.hp <= 0)
		{
			string sendStr = "Die|" + hitCS.socket.RemoteEndPoint.ToString();
			foreach (ClientState cs in MainClass.clients.Values)
			{
				MainClass.Send(cs, sendStr);
			}
		}
	}
}
class MainClass
{
	//监听Socket
	public static Socket listenfd;
	//客户端Socket及状态信息
	public static Dictionary<Socket, ClientState> clients = new Dictionary<Socket, ClientState>();

	public static void Main(string[] args)
	{
		//Socket
		listenfd = new Socket(AddressFamily.InterNetwork,
						SocketType.Stream, ProtocolType.Tcp);
		//Bind
		IPAddress ipAdr = IPAddress.Parse("127.0.0.1");
		IPEndPoint ipEp = new IPEndPoint(ipAdr, 8888);
		listenfd.Bind(ipEp);
		//Listen
		listenfd.Listen(0);
		Debug.Log("[服务器]启动成功");
		//checkRead
		List<Socket> checkRead = new List<Socket>();
		//主循环
		while (true)
		{
			//填充checkRead列表
			checkRead.Clear();
			checkRead.Add(listenfd);
			foreach (ClientState s in clients.Values)
			{
				checkRead.Add(s.socket);
			}
			//select
			Socket.Select(checkRead, null, null, 1000);
			//检查可读对象
			foreach (Socket s in checkRead)
			{
				if (s == listenfd)
				{
					ReadListenfd(s);
				}
				else
				{
					ReadClientfd(s);
				}
			}
		}
	}
	//读取Listenfd
	public static void ReadListenfd(Socket listenfd)
	{
		Debug.Log("Accept");
		Socket clientfd = listenfd.Accept();
		ClientState state = new ClientState();
		state.socket = clientfd;
		clients.Add(clientfd, state);
	}
	//读取Clientfd
	public static bool ReadClientfd(Socket clientfd)
	{
		ClientState state = clients[clientfd];
		//接收
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
			clients.Remove(clientfd);
			Debug.Log("Receive SocketException " + ex.ToString());
			return false;
		}
		//客户端关闭
		if (count <= 0)
		{
			MethodInfo mei = typeof(EventHandler).GetMethod("OnDisconnect");
			object[] ob = { state };
			mei.Invoke(null, ob);

			clientfd.Close();
			clients.Remove(clientfd);
			Debug.Log("Socket Close");
			return false;
		}
		//消息处理
		string recvStr =
				System.Text.Encoding.Default.GetString(state.readBuff, 0, count);
		string[] split = recvStr.Split('|');
		Debug.Log("Recv " + recvStr);
		string msgName = split[0];
		string msgArgs = split[1];
		string funName = "Msg" + msgName;
		MethodInfo mi = typeof(MsgHandler).GetMethod(funName);
		object[] o = { state, msgArgs };
		mi.Invoke(null, o);
		return true;
	}
	//发送
	public static void Send(ClientState cs, string sendStr)
	{
		byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);
		cs.socket.Send(sendBytes);
	}


}
public class ServerManager : MonoBehaviour
{
	//监听Socket
	public static Socket listenfd;
	//客户端Socket及状态信息
	public static Dictionary<Socket, ClientState> clients = new Dictionary<Socket, ClientState>();
	//
	List<Socket> checkRead;
	// Start is called before the first frame update
	void Start()
    {

		//Socket
		listenfd = new Socket(AddressFamily.InterNetwork,
						SocketType.Stream, ProtocolType.Tcp);
		//Bind
		IPAddress ipAdr = IPAddress.Parse("127.0.0.1");
		IPEndPoint ipEp = new IPEndPoint(ipAdr, 8888);
		listenfd.Bind(ipEp);
		//Listen
		listenfd.Listen(0);
		Debug.Log("[服务器]启动成功");
		//checkRead
		checkRead = new List<Socket>();
	}

    // Update is called once per frame
    void Update()
    {
		//填充checkRead列表
		checkRead.Clear();
		checkRead.Add(listenfd);
		foreach (ClientState s in clients.Values)
		{
			checkRead.Add(s.socket);
		}
		//select
		Socket.Select(checkRead, null, null, 1000);
		//检查可读对象
		foreach (Socket s in checkRead)
		{
			if (s == listenfd)
			{
				ReadListenfd(s);
			}
			else
			{
				ReadClientfd(s);
			}
		}
	}
	//读取Listenfd
	public static void ReadListenfd(Socket listenfd)
	{
		Debug.Log("Accept");
		Socket clientfd = listenfd.Accept();
		ClientState state = new ClientState();
		state.socket = clientfd;
		clients.Add(clientfd, state);
	}
	//读取Clientfd
	public static bool ReadClientfd(Socket clientfd)
	{
		ClientState state = clients[clientfd];
		//接收
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
			clients.Remove(clientfd);
			Debug.Log("Receive SocketException " + ex.ToString());
			return false;
		}
		//客户端关闭
		if (count <= 0)
		{
			MethodInfo mei = typeof(EventHandler).GetMethod("OnDisconnect");
			object[] ob = { state };
			mei.Invoke(null, ob);

			clientfd.Close();
			clients.Remove(clientfd);
			Debug.Log("Socket Close");
			return false;
		}
		//消息处理
		string recvStr =
				System.Text.Encoding.Default.GetString(state.readBuff, 0, count);
		string[] split = recvStr.Split('|');
		Debug.Log("Recv " + recvStr);
		string msgName = split[0];
		string msgArgs = split[1];
		string funName = "Msg" + msgName;
		MethodInfo mi = typeof(MsgHandler).GetMethod(funName);
		object[] o = { state, msgArgs };
		mi.Invoke(null, o);
		return true;
	}
	//发送
	public static void Send(ClientState cs, string sendStr)
	{
		byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);
		cs.socket.Send(sendBytes);
	}
}
