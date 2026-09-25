using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Lsof.Collectors;
using Lsof.Models;
using Lsof.Native;
using Lsof.Processes;

namespace Lsof.Tests.Integration;

[Collection("Native and console tests")]
public sealed class WindowsIntegrationTests
{
    [Fact]
    public void Handle_enumeration_finds_a_temporary_file_opened_by_the_current_process()
    {
        int processId = Environment.ProcessId;
        string path = Path.Combine(Path.GetTempPath(), $"lsof-test-{Guid.NewGuid():N}.tmp");

        try
        {
            using FileStream file = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            file.WriteByte(0x5a);
            file.Flush();

            List<WindowsHandleApi.HandleScanProgress> progress = new();
            WindowsHandleApi.EnumerationResult enumeration = WindowsHandleApi.EnumerateDiskFileHandles(
                ProcessSelection.FromProcessIds([processId]),
                progress.Add,
                TestContext.Current.CancellationToken);
            string fullPath = Path.GetFullPath(path);

            Assert.Contains(enumeration.Handles, handle =>
                handle.ProcessId == processId
                && string.Equals(Path.GetFullPath(handle.Path), fullPath, StringComparison.OrdinalIgnoreCase));
            Assert.NotEmpty(progress);
            Assert.Equal(progress[^1].TotalHandles, progress[^1].ProcessedHandles);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Handle_enumeration_honors_cancellation_before_querying_native_state()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            WindowsHandleApi.EnumerateDiskFileHandles(ProcessSelection.AllProcesses, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void Network_collection_finds_selected_ephemeral_sockets_and_excludes_unselected_ports()
    {
        HashSet<int> usedPorts = new();
        using TcpListener selectedTcp = CreateTcpListener(usedPorts);
        using UdpClient selectedUdp = CreateUdpClient(usedPorts);
        using TcpListener unselectedTcp = CreateTcpListener(usedPorts);
        using UdpClient unselectedUdp = CreateUdpClient(usedPorts);

        int selectedTcpPort = ((IPEndPoint)selectedTcp.LocalEndpoint).Port;
        int selectedUdpPort = ((IPEndPoint)selectedUdp.Client.LocalEndPoint!).Port;
        int unselectedTcpPort = ((IPEndPoint)unselectedTcp.LocalEndpoint).Port;
        int unselectedUdpPort = ((IPEndPoint)unselectedUdp.Client.LocalEndPoint!).Port;
        int processId = Environment.ProcessId;

        CollectionReport report = new();
        using ProcessCatalog catalog = ProcessCatalog.Create(report, TestContext.Current.CancellationToken);
        ProcessFilter filter = new([processId.ToString(CultureInfo.InvariantCulture)]);
        ProcessSelection selection = filter.ResolveSelection(catalog, report, TestContext.Current.CancellationToken);
        List<LsofEntry> entries = new();

        NetworkCollector.Collect(catalog, selection, [selectedTcpPort, selectedUdpPort], entries, report, TestContext.Current.CancellationToken);

        Assert.Contains(entries, entry =>
            entry.ProcessId == processId
            && entry.Protocol == "TCP"
            && entry.LocalPort == selectedTcpPort);
        Assert.Contains(entries, entry =>
            entry.ProcessId == processId
            && entry.Protocol == "UDP"
            && entry.LocalPort == selectedUdpPort);
        Assert.DoesNotContain(entries, entry =>
            entry.ProcessId == processId
            && entry.Protocol == "TCP"
            && entry.LocalPort == unselectedTcpPort);
        Assert.DoesNotContain(entries, entry =>
            entry.ProcessId == processId
            && entry.Protocol == "UDP"
            && entry.LocalPort == unselectedUdpPort);
    }

    private static TcpListener CreateTcpListener(HashSet<int> usedPorts)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            if (usedPorts.Add(port))
            {
                return listener;
            }

            listener.Stop();
        }

        throw new InvalidOperationException("Could not allocate a distinct ephemeral TCP port.");
    }

    private static UdpClient CreateUdpClient(HashSet<int> usedPorts)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            UdpClient client = new(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)client.Client.LocalEndPoint!).Port;
            if (usedPorts.Add(port))
            {
                return client;
            }

            client.Dispose();
        }

        throw new InvalidOperationException("Could not allocate a distinct ephemeral UDP port.");
    }
}
