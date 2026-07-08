using System.Buffers.Binary;
using System.Diagnostics;
using System.Threading;
using System.Xml;
using TcSysManRMLib;
using TwinCAT.Ads;
using TwinCAT.Ads.Extensions;
using TwinCAT.SystemService;

namespace TcAutomation
{
    public record RouteInfo(string Name, string NetId);

    public record StaticRouteInfo(string Name, string NetId, string Address, string Type, string Flags)
        : RouteInfo(Name, NetId);

    public enum TargetPlatform
    {
        TwinCATOS_ARMV7A,
        TwinCATOS_ARMV7M,
        TwinCATOS_ARMV8A,//Beckhoff RT Linux ARMV8-A
        TwinCATOS_x64, //TcBSD
        TwinCATOS_x64E,//Beckhoff RT Linux x64
        TwinCATRT_x64, //Windows Real-Time x64
        TwinCATRT_x86, //Windows Real-Time x86
    }

    [Flags]
    public enum CpuAffinity : ulong
    {
        None   = 0x0000000000000000,
        CPU1   = 0x0000000000000001,
        CPU2   = 0x0000000000000002,
        CPU3   = 0x0000000000000004,
        CPU4   = 0x0000000000000008,
        CPU5   = 0x0000000000000010,
        CPU6   = 0x0000000000000020,
        CPU7   = 0x0000000000000040,
        CPU8   = 0x0000000000000080,
        MaskAll = 0xFFFFFFFFFFFFFFFF
    }

    public class Automation : IDisposable
    {
        #region Lifecycle

        private ITcSysManager15? _sysman;

        /// <example>
        /// <code>
        /// using var automation = new Automation();
        /// </code>
        /// </example>
        public Automation()
        {
            MessageFilter.Register();

            var rm = (ITcSysManagerRM)Activator.CreateInstance(
                Type.GetTypeFromProgID("TcSysManagerRM")!)!;

            _sysman = rm.CreateSysManager15();
        }

        public void Dispose()
        {
            MessageFilter.Revoke();
        }

        public ITcSysManager15 SysManager => _sysman
            ?? throw new ObjectDisposedException(nameof(Automation));

        /// <summary>
        /// Looks up a tree item by its TwinCAT path (e.g. "TIID", "TIRT", "TIRR").
        /// </summary>
        /// <example>
        /// <code>
        /// ITcSmTreeItem ioRoot = automation.LookupTreeItem("TIID");
        /// </code>
        /// </example>
        public ITcSmTreeItem LookupTreeItem(string path) => SysManager.LookupTreeItem(path);

        #endregion

        #region Project

        /// <summary>
        /// Creates a blank TwinCAT configuration, discarding any existing project state.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.NewConfiguration();
        /// </code>
        /// </example>
        public void NewConfiguration() => SysManager.NewConfiguration();

        /// <summary>
        /// Sets the AMS NetId of the remote target to configure.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.SetTargetNetId("5.168.38.75.1.1");
        /// </code>
        /// </example>
        public void SetTargetNetId(string netId) => SysManager.SetTargetNetId(netId);

        /// <summary>
        /// Saves the current configuration to a .tsproj file on disk.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.SaveConfiguration(@"C:\temp\TwinCAT Project1.tsproj");
        /// </code>
        /// </example>
        public void SaveConfiguration(string fullPath) => SysManager.SaveConfiguration(fullPath);

        /// <summary>
        /// Activates the current configuration on the target, writing it to the boot folder.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.ActivateConfiguration();
        /// </code>
        /// </example>
        public void ActivateConfiguration() => SysManager.ActivateConfiguration();

        /// <summary>
        /// Sets the target platform for compilation.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.SetTargetPlatform(TargetPlatform.TwinCATOS_ARMV8A);
        /// </code>
        /// </example>
        public void SetTargetPlatform(TargetPlatform platform)
        {
            string platformString = platform switch
            {
                TargetPlatform.TwinCATOS_ARMV7A => "TwinCAT OS (ARMV7-A)",
                TargetPlatform.TwinCATOS_ARMV7M => "TwinCAT OS (ARMV7-M)",
                TargetPlatform.TwinCATOS_ARMV8A => "TwinCAT OS (ARMV8-A)",
                TargetPlatform.TwinCATOS_x64    => "TwinCAT OS (x64)",
                TargetPlatform.TwinCATOS_x64E   => "TwinCAT OS (x64-E)",
                TargetPlatform.TwinCATRT_x64    => "TwinCAT RT (x64)",
                TargetPlatform.TwinCATRT_x86    => "TwinCAT RT (x86)",
                _ => throw new ArgumentOutOfRangeException(nameof(platform))
            };

            ITcConfigManager configManager = (ITcConfigManager)(SysManager).ConfigurationManager;
            configManager.ActiveTargetPlatform = platformString;
        }

        /// <summary>
        /// Configures TwinCAT boot settings.
        /// </summary>
        /// <param name="autoRun">Start TwinCAT automatically on boot.</param>
        /// <param name="autoLogon">Enable automatic Windows logon.</param>
        /// <param name="logonUserName">Windows user name for auto-logon. Ignored when autoLogon is false.</param>
        /// <param name="logonPassword">Windows password for auto-logon. Ignored when autoLogon is false.</param>
        /// <param name="bootFileEncryptionType">Boot file encryption type, e.g. "None".</param>
        /// <example>
        /// <code>
        /// automation.ConfigureBootSettings(autoRun: true, autoLogon: false);
        /// </code>
        /// </example>
        public void ConfigureBootSettings(
            bool autoRun,
            bool autoLogon,
            string logonUserName = "",
            string logonPassword = "",
            string bootFileEncryptionType = "None")
        {
            ITcSmTreeItem systemConfig = LookupTreeItem("TIRC");

            StringWriter stringWriter = new StringWriter();
            using (XmlWriter writer = XmlWriter.Create(stringWriter))
            {
                writer.WriteStartElement("TreeItem");
                writer.WriteStartElement("System");
                writer.WriteStartElement("BootSettings");
                writer.WriteElementString("AutoRun", autoRun.ToString().ToLower());
                writer.WriteElementString("AutoLogon", autoLogon.ToString().ToLower());
                writer.WriteElementString("LogonUserName", logonUserName);
                writer.WriteElementString("LogonPassword", logonPassword);
                writer.WriteElementString("BootFileEncryptionType", bootFileEncryptionType);
                writer.WriteEndElement(); // BootSettings
                writer.WriteEndElement(); // System
                writer.WriteEndElement(); // TreeItem
            }

            systemConfig.ConsumeXml(stringWriter.ToString());
        }

        #endregion

        #region System

        /// <summary>
        /// Reads the current ADS state of the TwinCAT system service on the given target (port 10000).
        /// Returns <c>null</c> if the target is unreachable or the read times out.
        /// </summary>
        /// <example>
        /// <code>
        /// AdsState? state = automation.GetAdsState("5.168.38.75.1.1");
        /// if (state == AdsState.Run) { ... }
        /// </code>
        /// </example>
        public AdsState? GetAdsState(string netId)
        {
            try
            {
                using AdsClient adsClient = new AdsClient();
                adsClient.Connect(netId, 10000);
                StateInfo stateInfo = adsClient.ReadState();
                return stateInfo.AdsState;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Puts the TwinCAT system service into config mode via ADS (port 10000) and blocks
        /// until the target confirms it has reached <see cref="AdsState.Config"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.SetTargetNetId("5.168.38.75.1.1");
        /// automation.SetConfigMode(); // blocks until target is in config mode
        /// </code>
        /// </example>
        public void SetConfigMode()
        {
            using AdsClient adsClient = new AdsClient();
            adsClient.Connect(SysManager.GetTargetNetId(), 10000);
            adsClient.WriteControl(new StateInfo(AdsState.Reconfig, 0));
            adsClient.WaitForTargetState(
                new[] { AdsState.Config },
                pollingRate: TimeSpan.FromMilliseconds(adsClient.Timeout),
                waitTimeout: TimeSpan.FromSeconds(30),
                cancel: CancellationToken.None);
        }

        /// <summary>
        /// Enables or disables FreeRun mode on the target via ADS (port 300).
        /// The target must be in config mode before calling this method; if it is not,
        /// an error will be produced
        /// </summary>
        /// <example>
        /// <code>
        /// automation.SetFreeRunMode(enable: true);
        /// </code>
        /// </example>
        public void SetFreeRunMode(bool enable)
        {
            using AdsClient adsClient = new();
            adsClient.Connect(SysManager.GetTargetNetId(), 300);

            uint value = enable ? 1u : 0u;
            byte[] data = BitConverter.GetBytes(value);

            adsClient.TryWrite(
                indexGroup: 0x5000,
                indexOffset: 0x27,
                data
            ); // set FreeRun mode
        }

        /// <summary>
        /// Restarts TwinCAT into run mode on the target.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.StartRestartTwinCAT();
        /// </code>
        /// </example>
        public void StartRestartTwinCAT() => SysManager.StartRestartTwinCAT();

        /// <summary>
        /// Reboots the target system. A reboot is required for isolated CPU core
        /// changes (<see cref="SetOnTarget"/>) to take effect.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.RebootTarget();
        /// </code>
        /// </example>
        public void RebootTarget()
        {
            // TODO: implement target reboot (e.g. WriteControl on port 10000 with shutdown/restart flags)
            throw new NotImplementedException("Target reboot is not implemented yet.");
        }

        /// <summary>
        /// Blocks until the TwinCAT system service on the given target reaches Run state.
        /// </summary>
        public void WaitForRunMode(string netId)
        {
            using AdsClient adsClient = new AdsClient();
            adsClient.Connect(netId, 10000);
            adsClient.WaitForTargetState(
                new[] { AdsState.Run },
                pollingRate: TimeSpan.FromMilliseconds(adsClient.Timeout),
                waitTimeout: TimeSpan.FromSeconds(30),
                cancel: CancellationToken.None);
        }

        #region Routes

        /// <summary>
        /// Returns the currently active routes from TIRR (ActualRoutes).
        /// </summary>
        /// <example>
        /// <code>
        /// var routes = automation.GetCurrentRoutes();
        /// foreach (var r in routes) Console.WriteLine($"{r.Name} - {r.NetId}");
        /// </code>
        /// </example>
        public List<RouteInfo> GetCurrentRoutes()
            => ParseRoutes<RouteInfo>(
                "TreeItem/RoutePrj/ActualRoutes/Route",
                n => new RouteInfo(
                    n.SelectSingleNode("Name")!.InnerText,
                    n.SelectSingleNode("NetId")!.InnerText));

        /// <summary>
        /// Returns the configured static routes from TIRR (StaticRoutes).
        /// </summary>
        /// <example>
        /// <code>
        /// var routes = automation.GetStaticRoutes();
        /// foreach (var r in routes) Console.WriteLine($"{r.Name} ({r.Address}) - {r.NetId}");
        /// </code>
        /// </example>
        public List<StaticRouteInfo> GetStaticRoutes()
            => ParseRoutes<StaticRouteInfo>(
                "TreeItem/RoutePrj/StaticRoutes/Route",
                n => new StaticRouteInfo(
                    n.SelectSingleNode("Name")!.InnerText,
                    n.SelectSingleNode("NetId")!.InnerText,
                    n.SelectSingleNode("Address")?.InnerText ?? string.Empty,
                    n.SelectSingleNode("Type")?.InnerText ?? string.Empty,
                    n.SelectSingleNode("Flags")?.InnerText ?? string.Empty));

        /// <summary>
        /// Returns the project routes from TIRR (ProjectRoutes).
        /// </summary>
        /// <example>
        /// <code>
        /// var routes = automation.GetProjectRoutes();
        /// foreach (var r in routes) Console.WriteLine($"{r.Name} - {r.NetId}");
        /// </code>
        /// </example>
        public List<RouteInfo> GetProjectRoutes()
            => ParseRoutes<RouteInfo>(
                "TreeItem/RoutePrj/ProjectRoutes/Route",
                n => new RouteInfo(
                    n.SelectSingleNode("Name")!.InnerText,
                    n.SelectSingleNode("NetId")!.InnerText));

        #endregion

        #region Real-Time Settings

        /// <summary>
        /// Valid TwinCAT CPU core base times in descending order (µs).
        /// </summary>
        public static readonly IReadOnlyList<int> ValidBaseTimesUs = [1000, 500, 333, 250, 200, 125, 100, 83, 77, 71, 67, 62, 50];

        /// <summary>
        /// Returns the largest valid base time (µs) that evenly divides <paramref name="cycleTimeUs"/>,
        /// or 0 if no valid base time divides it.
        /// </summary>
        public static int ComputeBaseTime(int cycleTimeUs)
        {
            foreach (int bt in ValidBaseTimesUs)
            {
                if (cycleTimeUs % bt == 0)
                    return bt;
            }
            return 0;
        }

        /// <summary>
        /// Reads the total and isolated CPU counts from the <c>TargetCPUInfo</c> block in
        /// the TIRS node XML. This reflects the actual hardware configuration of the target.
        /// </summary>
        /// <returns>
        /// <c>TotalCpus</c>: value of <c>TargetCPUInfo/AvailabeCPUs</c>;
        /// <c>NonWindowsCpus</c>: value of <c>TargetCPUInfo/NonWindowsCPUs</c>.
        /// </returns>
        /// <example>
        /// <code>
        /// var (total, nonWindows) = automation.GetCpuCounts();
        /// Console.WriteLine($"Total: {total}, NonWindows (isolated): {nonWindows}");
        /// </code>
        /// </example>
        public (int TotalCpus, int NonWindowsCpus) GetCpuCounts()
        {
            ITcSmTreeItem realtimeSettings = LookupTreeItem("TIRS");

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(realtimeSettings.ProduceXml());

            XmlNode targetCpuInfo = xmlDoc.SelectSingleNode("/TreeItem/RTimeSetDef/TargetCPUInfo")
                ?? throw new InvalidOperationException("TargetCPUInfo node not found in TIRS XML.");

            int totalCpus = int.Parse(
                targetCpuInfo.SelectSingleNode("AvailabeCPUs")?.InnerText
                ?? throw new InvalidOperationException("AvailabeCPUs not found in TargetCPUInfo."));

            // A missing NonWindowsCPUs node simply means no isolated cores are configured.
            string? nonWindowsText = targetCpuInfo.SelectSingleNode("NonWindowsCPUs")?.InnerText;
            int nonWindowsCpus = string.IsNullOrEmpty(nonWindowsText) ? 0 : int.Parse(nonWindowsText);

            return (totalCpus, nonWindowsCpus);
        }

        /// <summary>
        /// Reads the base time for a specific CPU core from the real-time settings (TIRS).
        /// </summary>
        /// <param name="coreId">Zero-based CPU core index</param>
        /// <returns>Base time in microseconds</returns>
        /// <example>
        /// <code>
        /// int baseTimeUs = automation.GetCoreBaseTimeUs(coreId: 3);
        /// </code>
        /// </example>
        public int GetCoreBaseTimeUs(int coreId)
        {
            ITcSmTreeItem realtimeSettings = LookupTreeItem("TIRS");

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(realtimeSettings.ProduceXml());

            XmlNode? baseTimeNode = xmlDoc.SelectSingleNode(
                $"/TreeItem/RTimeSetDef/CPUs/CPU[@id='{coreId}']/BaseTime");

            if (baseTimeNode == null)
                throw new InvalidOperationException($"No BaseTime found for CPU core {coreId} in TIRS.");

            // BaseTime XML field is in 100ns units; convert to microseconds
            return int.Parse(baseTimeNode.InnerText) / 10;
        }

        /// <summary>
        /// Sets the base time for a specific CPU core in the real-time settings.
        /// </summary>
        /// <param name="coreId">Zero-based CPU core index</param>
        /// <param name="baseTimeUs">Base time in microseconds, e.g. 1000 = 1ms</param>
        /// <example>
        /// <code>
        /// automation.SetCoreBaseTime(coreId: 3, baseTimeUs: 1000); // 1ms on core 3
        /// </code>
        /// </example>
        public void SetCoreBaseTime(int coreId, int baseTimeUs)
        {
            ITcSmTreeItem realtimeSettings = LookupTreeItem("TIRS");

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(realtimeSettings.ProduceXml());

            XmlNode? baseTimeNode = xmlDoc.SelectSingleNode(
                $"/TreeItem/RTimeSetDef/CPUs/CPU[@id='{coreId}']/BaseTime");

            if (baseTimeNode != null)
            {
                // BaseTime XML field is in 100ns units; convert from microseconds
                baseTimeNode.InnerText = (baseTimeUs * 10).ToString();
                realtimeSettings.ConsumeXml(xmlDoc.InnerXml);
            }
        }

        /// <summary>
        /// Configures the real-time CPU core settings.
        /// </summary>
        /// <param name="totalCpuCount">
        ///   Total number of logical CPU cores (shared + isolated). Written to <c>MaxCPUs</c>.
        /// </param>
        /// <param name="isolatedCores">
        ///   Number of cores isolated from Windows (<c>NonWindowsCPUs</c> attribute on <c>MaxCPUs</c>).
        ///   0 for shared-only configurations.
        /// </param>
        /// <param name="cores">
        ///   Per-core settings as (coreId, baseTimeUs) where baseTimeUs is in microseconds
        ///   (e.g. 1000 = 1ms). Only these cores appear in the <c>CPUs</c> block and affinity
        ///   mask — no intermediate cores are inserted.
        /// </param>
        /// <example>
        /// <code>
        /// // 4-core system, isolate core 3 for TwinCAT with a 1ms base time
        /// automation.ConfigureCpuCores(totalCpuCount: 4, isolatedCores: 1, (coreId: 3, baseTimeUs: 1000));
        /// </code>
        /// </example>
        public void ConfigureCpuCores(int totalCpuCount, int isolatedCores, params (int coreId, int baseTimeUs)[] cores)
        {
            ITcSmTreeItem realtimeSettings = LookupTreeItem("TIRS");

            ulong affinityMask = 0;
            foreach (var (coreId, _) in cores)
                affinityMask |= 1UL << coreId;

            StringWriter stringWriter = new StringWriter();
            using (XmlWriter writer = XmlWriter.Create(stringWriter))
            {
                writer.WriteStartElement("TreeItem");
                writer.WriteStartElement("RTimeSetDef");

                writer.WriteStartElement("MaxCPUs");
                if (isolatedCores > 0)
                    writer.WriteAttributeString("NonWindowsCPUs", isolatedCores.ToString());
                writer.WriteString(totalCpuCount.ToString());
                writer.WriteEndElement(); // MaxCPUs

                writer.WriteElementString("Affinity", $"#x{affinityMask:x}");

                writer.WriteStartElement("CPUs");
                foreach (var (coreId, baseTimeUs) in cores)
                {
                    writer.WriteStartElement("CPU");
                    writer.WriteAttributeString("id", coreId.ToString());
                    // BaseTime XML field is in 100ns units; convert from microseconds
                    writer.WriteElementString("BaseTime", (baseTimeUs * 10).ToString());
                    writer.WriteEndElement(); // CPU
                }
                writer.WriteEndElement(); // CPUs

                writer.WriteEndElement(); // RTimeSetDef
                writer.WriteEndElement(); // TreeItem
            }

            realtimeSettings.ConsumeXml(stringWriter.ToString());
        }

        /// <summary>
        /// Sets the number of isolated CPU cores directly on the target via the
        /// <c>SetOnTarget</c> method of the real-time settings (TIRS). The target
        /// applies the new core isolation immediately, without activating a configuration.
        /// </summary>
        /// <param name="isolatedCores">
        ///   Number of cores isolated from Windows (<c>NonWindowsCPUs</c> attribute on <c>MaxCPUs</c>).
        ///   0 for shared-only configurations.
        /// </param>
        /// <example>
        /// <code>
        /// automation.SetOnTarget(isolatedCores: 2);
        /// </code>
        /// </example>
        public void SetOnTarget(int isolatedCores)
        {
            ITcSmTreeItem realtimeSettings = LookupTreeItem("TIRS");

            var (totalCpus, _) = GetCpuCounts();

            string nonWindowsAttr = isolatedCores > 0 ? $" NonWindowsCPUs=\"{isolatedCores}\"" : string.Empty;

            string xml =
                "<TreeItem>" +
                "<RTimeSetDef>" +
                $"<MaxCPUs{nonWindowsAttr}>{totalCpus}</MaxCPUs>" +
                "<Methods>" +
                "<SetOnTarget>true</SetOnTarget>" +
                "</Methods>" +
                "</RTimeSetDef>" +
                "</TreeItem>";

            realtimeSettings.ConsumeXml(xml);

            // GetLastXmlError returns the item path / message of the last erroneous
            // ConsumeXml call; ConsumeXml itself does not report SetOnTarget failures.
            string xmlError = realtimeSettings.GetLastXmlError();
            if (!string.IsNullOrEmpty(xmlError))
                throw new InvalidOperationException($"SetOnTarget failed: {xmlError}");
        }

        #endregion

        #endregion

        #region Tasks

        /// <summary>
        /// Creates a new real-time task with image under the TIRT node.
        /// </summary>
        /// <example>
        /// <code>
        /// ITcSmTreeItem task = automation.CreateTaskWithImage("EtherCAT Task");
        /// </code>
        /// </example>
        public ITcSmTreeItem CreateTaskWithImage(string taskName)
        {
            ITcSmTreeItem tasks = LookupTreeItem("TIRT");
            return tasks.CreateChild(taskName, 0, null, null);
        }

        /// <summary>
        /// Creates a new real-time task without image under the TIRT node.
        /// </summary>
        /// <example>
        /// <code>
        /// ITcSmTreeItem task = automation.CreateTaskWithoutImage("Background Task");
        /// </code>
        /// </example>
        public ITcSmTreeItem CreateTaskWithoutImage(string taskName)
        {
            ITcSmTreeItem tasks = LookupTreeItem("TIRT");
            return tasks.CreateChild(taskName, 1, null, null);
        }

        /// <summary>
        /// Adds an input variable to a task image.
        /// </summary>
        /// <example>
        /// <code>
        /// ITcSmTreeItem devStateVar = automation.AddTaskInputVariable(task, "Master1 DevState", "DINT");
        /// </code>
        /// </example>
        public ITcSmTreeItem AddTaskInputVariable(ITcSmTreeItem task, string variableName, string dataType)
        {
            ITcSmTreeItem inputs = task.LookupChild("Inputs");
            return inputs.CreateChild(variableName, -1, null, dataType);
        }

        /// <summary>
        /// Adds an output variable to a task image.
        /// </summary>
        /// <example>
        /// <code>
        /// ITcSmTreeItem controlVar = automation.AddTaskOutputVariable(task, "ControlWord", "WORD");
        /// </code>
        /// </example>
        public ITcSmTreeItem AddTaskOutputVariable(ITcSmTreeItem task, string variableName, string dataType)
        {
            ITcSmTreeItem outputs = task.LookupChild("Outputs");
            return outputs.CreateChild(variableName, -1, null, dataType);
        }

        /// <summary>
        /// Sets the cycle time of a task.
        /// </summary>
        /// <param name="task">The task ITcSmTreeItem</param>
        /// <param name="cycleTimeUs">Cycle time in microseconds, e.g. 1000 = 1ms, 10000 = 10ms</param>
        /// <example>
        /// <code>
        /// automation.SetTaskCycleTime(task, cycleTimeUs: 1000); // 1ms cycle
        /// </code>
        /// </example>
        public void SetTaskCycleTime(ITcSmTreeItem task, int cycleTimeUs)
        {
            // TwinCAT internally uses 100ns units
            // TaskDef/CycleTime:             microseconds * 10
            // Context/CycleTime:             microseconds * 1000
            long taskDefCycleTime    = cycleTimeUs * 10L;
            long contextCycleTime    = cycleTimeUs * 1000L;

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(task.ProduceXml());

            XmlNode taskDefCycleTimeNode = xmlDoc.SelectSingleNode("TreeItem/TaskDef/CycleTime")!;
            taskDefCycleTimeNode.InnerText = taskDefCycleTime.ToString();

            XmlNode contextCycleTimeNode = xmlDoc.SelectSingleNode("TreeItem/TcModuleInstance/Module/Contexts/Context/CycleTime")!;
            contextCycleTimeNode.InnerText = contextCycleTime.ToString();

            task.ConsumeXml(xmlDoc.InnerXml);
        }

        /// <summary>
        /// Assigns a task to a specific CPU core by zero-based core id.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.AssignTaskToCpu(task, coreId: 3);
        /// </code>
        /// </example>
        public void AssignTaskToCpu(ITcSmTreeItem task, int coreId)
            => AssignTaskToCpu(task, (CpuAffinity)(1UL << coreId));

        /// <summary>
        /// Assigns a task to a specific CPU core via its affinity mask.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.AssignTaskToCpu(task, CpuAffinity.CPU4);
        /// </code>
        /// </example>
        public void AssignTaskToCpu(ITcSmTreeItem task, CpuAffinity affinity)
        {
            string affinityString = $"#x{(ulong)affinity:x16}";

            StringWriter stringWriter = new StringWriter();
            using (XmlWriter writer = XmlWriter.Create(stringWriter))
            {
                writer.WriteStartElement("TreeItem");
                writer.WriteStartElement("TaskDef");
                writer.WriteElementString("CpuAffinity", affinityString);
                writer.WriteEndElement(); // TaskDef
                writer.WriteEndElement(); // TreeItem
            }

            task.ConsumeXml(stringWriter.ToString());
        }

        #endregion

        #region I/O

        /// <summary>
        /// Scans for devices under TIID, creates a child item for each found device,
        /// and returns the list.
        /// </summary>
        /// <example>
        /// <code>
        /// var devices = automation.ScanAndCreateDevices();
        /// </code>
        /// </example>
        public List<ITcSmTreeItem> ScanAndCreateDevices()
        {
            ITcSmTreeItem ioItem = LookupTreeItem("TIID");
            string scannedXml = ioItem.ProduceXml(false);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(scannedXml);
            XmlNodeList deviceNodes = xmlDoc.SelectNodes(
                "TreeItem/DeviceGrpDef/FoundDevices/Device")!;

            var devices = new List<ITcSmTreeItem>();
            int count = 0;

            foreach (XmlNode node in deviceNodes)
            {
                int subType = int.Parse(node.SelectSingleNode("ItemSubType")!.InnerText);
                XmlNode addressNode = node.SelectSingleNode("AddressInfo")!;

                ITcSmTreeItem device = ioItem.CreateChild(
                    $"Device_{++count}", subType, string.Empty, null);

                string xml = $"<TreeItem><DeviceDef>{addressNode.OuterXml}</DeviceDef></TreeItem>";
                device.ConsumeXml(xml);
                devices.Add(device);
            }

            return devices;
        }

        /// <summary>
        /// Scans for devices under TIID where ItemSubTypeName is "EtherCAT Master",
        /// creates a child item for each, and returns the list.
        /// </summary>
        /// <example>
        /// <code>
        /// var masters = automation.ScanAndCreateEtherCatMasters();
        /// </code>
        /// </example>
        public List<ITcSmTreeItem> ScanAndCreateEtherCatMasters()
        {
            ITcSmTreeItem ioItem = LookupTreeItem("TIID");
            string scannedXml = ioItem.ProduceXml(false);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(scannedXml);
            XmlNodeList deviceNodes = xmlDoc.SelectNodes(
                "TreeItem/DeviceGrpDef/FoundDevices/Device[ItemSubTypeName='EtherCAT Master']")!;

            var devices = new List<ITcSmTreeItem>();
            int count = 0;

            foreach (XmlNode node in deviceNodes)
            {
                int subType = int.Parse(node.SelectSingleNode("ItemSubType")!.InnerText);
                XmlNode addressNode = node.SelectSingleNode("AddressInfo")!;

                ITcSmTreeItem device = ioItem.CreateChild(
                    $"EtherCAT Master {++count}", subType, string.Empty, null);

                string xml = $"<TreeItem><DeviceDef>{addressNode.OuterXml}</DeviceDef></TreeItem>";
                device.ConsumeXml(xml);
                devices.Add(device);
            }

            return devices;
        }

        /// <summary>
        /// Triggers a box scan on the given device and returns its child items.
        /// </summary>
        /// <example>
        /// <code>
        /// var boxes = automation.ScanBoxes(master).ToList();
        /// </code>
        /// </example>
        public IEnumerable<ITcSmTreeItem> ScanBoxes(ITcSmTreeItem device)
        {
            const string xml = "<TreeItem><DeviceDef><ScanBoxes>1</ScanBoxes></DeviceDef></TreeItem>";
            device.ConsumeXml(xml);

            foreach (ITcSmTreeItem box in device)
                yield return box;
        }

        /// <summary>
        /// Recursively collects all items of the given ItemType under the provided root.
        /// </summary>
        /// <example>
        /// <code>
        /// var slaves = automation.FindItemsByType(master, itemType: 5);
        /// </code>
        /// </example>
        public List<ITcSmTreeItem> FindItemsByType(ITcSmTreeItem root, int itemType)
        {
            var results = new List<ITcSmTreeItem>();
            CollectByType(root, itemType, results);
            return results;
        }

        /// <summary>
        /// Recursively collects all items of the given ItemType across a list of roots.
        /// </summary>
        /// <example>
        /// <code>
        /// var slaves = automation.FindItemsByType(boxes, itemType: 5);
        /// </code>
        /// </example>
        public List<ITcSmTreeItem> FindItemsByType(IEnumerable<ITcSmTreeItem> roots, int itemType)
        {
            var results = new List<ITcSmTreeItem>();
            foreach (ITcSmTreeItem root in roots)
                CollectByType(root, itemType, results);
            return results;
        }

        /// <summary>
        /// Returns the DevState item from the Inputs of the given EtherCAT master device.
        /// </summary>
        /// <example>
        /// <code>
        /// ITcSmTreeItem devState = automation.GetMasterDevState(master);
        /// </code>
        /// </example>
        public ITcSmTreeItem GetMasterDevState(ITcSmTreeItem ecMaster)
        {
            return ecMaster.LookupChild("Inputs").LookupChild("DevState");
        }

        /// <summary>
        /// Links two ITcSmTreeItems using their PathNames.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.LinkVariables(devStateVar, automation.GetMasterDevState(master));
        /// </code>
        /// </example>
        public void LinkVariables(ITcSmTreeItem source, ITcSmTreeItem target)
        {
            SysManager.LinkVariables(source.PathName, target.PathName);
        }

        /// <summary>
        /// Finds all Master-Sync Image items directly under the given EtherCAT master
        /// and enables the ADS server on each one.
        /// </summary>
        /// <example>
        /// <code>
        /// automation.EnableAdsServer(master); // uses default port 27905
        /// </code>
        /// </example>
        public void EnableAdsServer(ITcSmTreeItem ecMaster, int port = 27905)
        {
            foreach (ITcSmTreeItem child in ecMaster)
            {
                if (child.ItemSubTypeName != "Master-Sync Image")
                    continue;

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(child.ProduceXml());

                // append <ImageDef><AdsServer>...</AdsServer></ImageDef> if not already present
                XmlNode treeItem = xmlDoc.SelectSingleNode("TreeItem")!;

                XmlNode? existing = treeItem.SelectSingleNode("ImageDef/AdsServer");
                if (existing != null)
                    continue;

                XmlElement imageDef    = xmlDoc.CreateElement("ImageDef");
                XmlElement adsServer   = xmlDoc.CreateElement("AdsServer");
                XmlElement portEl      = xmlDoc.CreateElement("Port");
                XmlElement symbolsEl   = xmlDoc.CreateElement("CreateSymbols");

                portEl.InnerText    = port.ToString();
                symbolsEl.InnerText = "true";

                adsServer.AppendChild(portEl);
                adsServer.AppendChild(symbolsEl);
                imageDef.AppendChild(adsServer);
                treeItem.AppendChild(imageDef);

                child.ConsumeXml(xmlDoc.InnerXml);
            }
        }

        /// <summary>
        /// Returns the ItemId of the given IO device.
        /// </summary>
        /// <example>
        /// <code>
        /// int id = automation.GetDeviceId(master);
        /// </code>
        /// </example>
        public int GetDeviceId(ITcSmTreeItem device)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(device.ProduceXml());
            return int.Parse(xmlDoc.SelectSingleNode("TreeItem/ItemId")!.InnerText);
        }

        #endregion

        #region Private Helpers

        private List<T> ParseRoutes<T>(string xpath, Func<XmlNode, T> selector)
        {
            ITcSmTreeItem routes = LookupTreeItem("TIRR");
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(routes.ProduceXml());

            var result = new List<T>();
            foreach (XmlNode node in xmlDoc.SelectNodes(xpath)!)
            {
                if (node.SelectSingleNode("NetId")?.InnerText == "255.255.255.255.255.255")
                    continue;
                result.Add(selector(node));
            }
            return result;
        }

        private static void CollectByType(ITcSmTreeItem item, int itemType, List<ITcSmTreeItem> results)
        {
            if (item.ItemType == itemType)
                results.Add(item);

            foreach (ITcSmTreeItem child in item)
                CollectByType(child, itemType, results);
        }

        #endregion
    }
}
