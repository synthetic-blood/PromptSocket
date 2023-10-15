using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace Chat;
public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

		LogView.ItemsSource = LogRegister;
		SavedEndpoints = new();


		SavedEndpointsPKR.ItemsSource = SavedEndpoints;

		UpdateSaveLAB();

		//disables ClearLogBUN when there's no log
		LogRegister.CollectionChanged += (sender, e) => ClearLogBUN.IsEnabled = LogRegister.Count > 0;
		ClearLogBUN.Clicked += (sender, e) => LogRegister.Clear();

		Loaded += (sender, e) =>
			{
				if (!File.Exists(SavedEndpointsFilePath))
				{
					//welcome message
					Log("hi! \n1. swipe host/port field to view save options\n\t↓ save\n\t→ pick saved endpoint\n\t← remove or clean fields\n2. tap on log to copy");
					File.Create(SavedEndpointsFilePath);
				}
				else
					DeserializeEndpointsFromFile();
			};
	}

	private ObservableCollection<string> SavedEndpoints;

	private void TransferBUN_Clicked(object sender, EventArgs e)
	{
		SendMessage();
	}
	private void IsMessageValid() => TransferBUN.IsEnabled = MessageENT.Text != string.Empty && AppSocket != null;
	private void MessageENT_TextChanged(object sender, TextChangedEventArgs e)
	{
		IsMessageValid();
	}
	private void SavedEndpointsPKR_SelectedIndexChanged(object sender, EventArgs e)
	{
		EndpointSWP.Close();
		LoadEnpointIntoEntries();
	}

	//internal app use
	private void LoadEnpointIntoEntries()
	{
		if (SavedEndpointsPKR.SelectedIndex == -1)
			return;
		HostENT.TextChanged -= Endpoint_TextChanged;
		PortENT.TextChanged -= Endpoint_TextChanged;

		HostENT.Text = Endpoint()[0];
		PortENT.Text = Endpoint()[1];

		HostENT.TextChanged += Endpoint_TextChanged;
		PortENT.TextChanged += Endpoint_TextChanged;

		SavedEndpointsPKR.Unfocus();

		UpdateSaveLAB();
	}


	private void SaveEndpoint(object sender, EventArgs e)
	{
		if (IsEntriesValid() && !SavedEndpoints.Contains(EndpointFromEntries()))
		{
			SavedEndpoints.Add(EndpointFromEntries());
			SerializeEndpointsToFile();
			UpdateSaveLAB();
		}
	}
	private void RemoveSavedEndpoint(object sender, EventArgs e)
	{
		int index = SavedEndpointsPKR.SelectedIndex;
		if (index != -1)
		{
			SavedEndpoints.RemoveAt(index);
			SerializeEndpointsToFile();
		}
		ClearEntries();
	}
	private void UpdateSaveLAB()
	{
		SocketSwitch.IsEnabled = IsEntriesValid();
		if (!IsEntriesValid())
			SaveLAB.Text = "Invalid Field ★";
		else
			if (SavedEndpoints.Contains(EndpointFromEntries()))
			SaveLAB.Text = "Already Saved ★";
		else
			SaveLAB.Text = "Save ★";
	}

	//Endpoint Fields
	private void ClearEntries()
	{
		HostENT.Text = PortENT.Text = string.Empty;
	}
	private void Endpoint_TextChanged(object sender, TextChangedEventArgs e)
	{
		PortENT.TextColor = Color.Parse(IsPortValid() ? "White" : "Gray");
		UpdateSaveLAB();
	}
	private string EndpointFromEntries() => HostENT.Text + ':' + PortENT.Text;
	private string[] Endpoint()
	{
		return (SavedEndpointsPKR.SelectedIndex != -1 ? (SavedEndpointsPKR.SelectedItem as string) : EndpointFromEntries()).Split(':', StringSplitOptions.TrimEntries);
	}
	bool IsPortValid()
	{
		uint port = PortENT.Text != string.Empty ? Convert.ToUInt32(Endpoint()[1]) : 0;
		return Endpoint()[1] != string.Empty ? (isServer ? port >= 1024 : port <= 65535) : false;
	}
	bool IsEntriesValid() => (isServer || HostENT.Text != string.Empty) && PortENT.Text != string.Empty && IsPortValid();
	//Log Support
	private void Log(string message) => MainThread.BeginInvokeOnMainThread(() =>
										{
											LogRegister.Add(message);
											LogView.ScrollTo(LogRegister.Count);
										});

	private ObservableCollection<string> LogRegister = new();
	//Net Support
	private void ChangeState(System.EventHandler Handler, string Text)
	{
		SocketSwitch.Clicked -= SocketSwitchConnect_Clicked;
		SocketSwitch.Clicked -= SocketSwitchCancel_Clicked;
		SocketSwitch.Clicked -= SocketSwitchDisconnect_Clicked;
		SocketSwitch.Clicked -= SocketSwitchListen_Clicked;
		SocketSwitch.Clicked -= SocketSwitchShutdownServer_Clicked;
		SocketSwitch.Clicked += Handler;
		SocketSwitch.Text = Text;
	}
	private void SocketSwitchConnect_Clicked(object sender, EventArgs e)
	{
		Connect();
	}
	private async void Connect()
	{
		if (ConnectionToken.Token.IsCancellationRequested)
			return;

		Disconnect();

		Log("┌@" + EndpointFromEntries());
		AppSocket = new(SocketType.Stream, ProtocolType.Tcp);
		WaitingACI.IsRunning = true;
		ChangeState(SocketSwitchCancel_Clicked, "Cancel");
		try
		{
			await AppSocket.ConnectAsync(Endpoint()[0], Convert.ToUInt16(Endpoint()[1]), ConnectionToken.Token);

			IsMessageValid();

			StreamThread = new(ReceiveMessage);
			StreamThread.Start();

			Log("└Connected");
			ChangeState(SocketSwitchDisconnect_Clicked, "Disconnect");
		}
		catch (Exception ex)
		{
			Log("─" + ex.Message);
			ChangeState(SocketSwitchConnect_Clicked, "Connect");
			Disconnect();
		}
		finally
		{
			WaitingACI.IsRunning = false;
		}
	}
	private void SocketSwitchCancel_Clicked(object sender, EventArgs e)
	{
		Log("└Cancelled");
		ChangeState(SocketSwitchConnect_Clicked, "Connect");
		CancelConnection();
	}
	private void CancelConnection()
	{
		ConnectionToken.Cancel(true);
		RestartConnectionToken();
	}
	private void SocketSwitchShutdownServer_Clicked(object sender, EventArgs e)
	{
		Shutdown();
	}
	void Shutdown()
	{
		Log("[Server] Shutdown");
		PortENT.IsEnabled = true;
		Clients.Clear();
		Disconnect();
		ChangeState(SocketSwitchListen_Clicked, "Listen");
	}
	private void SocketSwitchDisconnect_Clicked(object sender, EventArgs e)
	{
		Log("─Disconnected");
		Disconnect();
		ChangeState(SocketSwitchConnect_Clicked, "Connect");
	}

	private void Disconnect()
	{
		if (AppSocket != null)
		{
			TransferBUN.IsEnabled = false;
			RestartStreamToken();
			AppSocket.Close();
			AppSocket = null;
		}
	}

	private void ReceiveMessage()
	{
		byte[] pendingRead = new byte[1024];
		{
			try
			{
				for (; ; )
				{
					int bytesReceived = AppSocket.Receive(pendingRead);
					if (bytesReceived == 0)
						return;

					Log(Encoding.ASCII.GetString(pendingRead, 0, bytesReceived));

					Array.Clear(pendingRead);
				}
			}
			catch (Exception ex)
			{
				Log(ex.Message);
			}
		}
	}
	void SocketSwitchListen_Clicked(object sender, EventArgs e)
	{
		Log("[Port " + PortENT.Text + "] Listening");
		Listen();
		PortENT.IsEnabled = false;
	}
	private async void SendMessage()
	{
		byte[] pendingWrite = Encoding.UTF8.GetBytes(MessageENT.Text);
		try
		{
			if (isServer)
				foreach (Socket client in Clients)
					await client.SendAsync(pendingWrite, StreamToken.Token);
			else
				await AppSocket.SendAsync(pendingWrite, StreamToken.Token);
			Log(MessageENT.Text);
			MessageENT.Text = string.Empty;
		}
		catch (Exception ex)
		{
			Log(ex.Message);
			Disconnect();
		}
	}
	private void RestartConnectionToken() => ConnectionToken = new();
	private void RestartStreamToken() => StreamToken = new();

	private Thread StreamThread;
	Thread AcceptThread;
	private CancellationTokenSource ConnectionToken = new(),
									StreamToken = new();
	private Socket AppSocket;

	//file support
	private void SerializeEndpointsToFile()
	{
		File.WriteAllText(SavedEndpointsFilePath, "");
		File.AppendAllLines(SavedEndpointsFilePath, SavedEndpoints);
	}
	private void DeserializeEndpointsFromFile()
	{
		foreach (string endpoint in File.ReadAllLines(SavedEndpointsFilePath))
			SavedEndpoints.Add(endpoint);
	}
	void Listen()
	{
		int port = Convert.ToInt16(PortENT.Text);

		AppSocket = new(SocketType.Stream, ProtocolType.Tcp);
		AppSocket.Bind(new IPEndPoint(IPAddress.Any, Convert.ToInt16(PortENT.Text)));
		AppSocket.Listen();

		AcceptThread = new(AcceptConnectionThread);
		AcceptThread.Start();
		ChangeState(SocketSwitchShutdownServer_Clicked, "Shutdown");
	}
	void AcceptConnectionThread()
	{
		Log("[Server] Thread running in background");
		try
		{
			for (; ; )
			{
				Socket ClientSocket = AppSocket.Accept();
				Log("[+] " + (ClientSocket.RemoteEndPoint as IPEndPoint).Address.ToString());
				UpdatePeerCounter();
				Clients.Add(ClientSocket);
			}
		}
		catch (Exception ex)
		{
			Log("[Server] " + ex.Message);
		}
	}
	private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
	{
		await Clipboard.Default.SetTextAsync((sender as Label).Text);
		Label thisLog = (sender as Label);
		thisLog.Opacity = 0.2;
		await (sender as Label).FadeTo(1);
	}
	private string SavedEndpointsFilePath = Path.Combine(FileSystem.AppDataDirectory, "SavedEndpoints");

	private void ChangeSocketRole_CheckedChanged(object sender, CheckedChangedEventArgs e)
	{
		bool isServer = ((sender as RadioButton).Content as string) == "Server";
		Log(((sender as RadioButton).Content as string));
		HostENT.IsEnabled = !isServer;
		SocketSwitch.Text = isServer ? "Listen" : "Connect";

		SettingsSWI.IsVisible = false;
		SettingsSWI.IsVisible = true;

		ServerSettings.IsVisible = isServer;
		if (isServer)
			HostENT.Text = "---.---.---.---";
		else ClearEntries();
	}
	private void EnableClient()
	{
		HostENT.Placeholder = "Host";
		HostENT.IsEnabled = PortENT.IsEnabled = true;

		SocketSwitch.Text = "Connect";
		ChangeState(SocketSwitchConnect_Clicked, "Connect");
	}
	void EnableServer()
	{
		HostENT.IsEnabled = false;
		HostENT.Placeholder = "---.---.---.---";

		ChangeState(SocketSwitchListen_Clicked, "Listen");
	}

	private void SwitchMode(object sender, EventArgs e)
	{
		ClearEntries();

		isServer = (sender as Button).Text == "[Server";
		ServerBUN.IsEnabled = !isServer;
		ClientBUN.IsEnabled = isServer;

		if (isServer) EnableServer(); else EnableClient();
	}


	public string AppMode => "Client";

	async void UpdatePeerCounter() => await MainThread.InvokeOnMainThreadAsync(() => { PeerCounter.Text = (peersConnected += 1) + " peers(s)"; });
	private byte peersConnected = 0;
	List<Socket> Clients = new();
	private bool isServer = false;
}