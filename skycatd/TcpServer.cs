using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace skycatd
{
  public class TcpServer
  {
    private readonly int Port;
    private readonly CommandInterpreter Interpreter;
    private readonly ILogger Logger;
    private TcpListener? Listener;
    private readonly ConcurrentBag<TcpClient> ActiveClients = new();

    public TcpServer(int port, CommandInterpreter interpreter, ILogger logger)
    {
      Port = port;
      Interpreter = interpreter;
      Logger = logger;
    }

    public void Start()
    {
      if (IsListening()) return;

      try
      {
        Listener = new TcpListener(IPAddress.Any, Port);
        Listener.Start();

        // start accepting clients in the background
        Task.Run(async () =>
        {
          while (Listener != null)
            try
            {
              var client = await Listener.AcceptTcpClientAsync();
              _ = Task.Run(() => HandleClient(client));
            }
            catch (Exception ex)
            {
              // Stop() called
              if (Listener == null) break;
              // failure
              else Logger.LogError($"TCP server stopped: {ex.Message}");
            }
        });
      }
      catch (Exception)
      {
        Listener = null;
        throw;
      }
    }

    int NextId = 1;
    private readonly object Lock = new();

    private void HandleClient(TcpClient client)
    {
      int id = NextId++;
      ActiveClients.Add(client);
      var endPoint = client.Client.RemoteEndPoint;
      Logger.LogInformation($"Client #{id} connected: {endPoint} ({ActiveClients.Count} connected clients)");
      using var stream = client.GetStream();
      using var reader = new StreamReader(stream, Encoding.ASCII);
      using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

      //writer.WriteLine("Welcome to skycatd. Type commands:");

      string? line;
      while ((line = reader.ReadLine()) != null)
      {
        Logger.LogDebug($"Received from client #{id}: '{line}'");

        string response;
        lock (Lock) response = Interpreter.Execute(line);

        Logger.LogDebug($"  Replying to client #{id}: {AddDescription(response)}");
        writer.Write(response + "\n");
      }

      client.Close();
      ActiveClients.TryTake(out _);
      Logger.LogInformation($"Client  #{id} disconnected: {endPoint} ({ActiveClients.Count} connected clients)");
    }

    private string AddDescription(string response)
    {
      return response switch
      {
        "RPRT 0" => $"'{response}' (OK)",
        "RPRT -1" => $"'{response}' (invalid parameter)",
        "RPRT -5" => $"'{response}' (I/O timeout)",
        "RPRT -6" => $"'{response}' (I/O error)",
        "RPRT -7" => $"'{response}' (internal error)",
        "RPRT -9" => $"'{response}' (command rejected by the radio)",
        "RPRT -11" => $"'{response}' (function not available)",
        _ when response.StartsWith("RPRT ") => $"'{response}' (Unknown RPRT code)",
        _ => $"'{response}'"
      };
    }

    public void Stop()
    {
      if (!IsListening()) return;

      foreach (var client in ActiveClients)
        try
        {

          client.Close();
        }
        catch { }
      ActiveClients.Clear();

      Listener?.Stop();
      Listener = null;
      Logger.LogInformation("TCP Server stopped.");
    }

    public bool IsListening()
    {
      return Listener != null && Listener.Server.IsBound;
    }
  }
}
