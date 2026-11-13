using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace CuoHandshakeFix
{
    /// <summary>
    /// Loaded by the IL hook inside RazorEnhanced PacketHandlers.Initialize.
    /// Uses reflection so it stays compatible across RE builds without a hard reference.
    /// </summary>
    public static class Bootstrap
    {
        private const byte ChallengePacketId = 0xD5;
        private const short ChallengePacketLength = 9;

        private static readonly object Gate = new object();
        private static bool _applied;
        private static string _logPath;

        public static void Apply()
        {
            lock (Gate)
            {
                if (_applied)
                    return;

                try
                {
                    _logPath = ResolveLogPath();
                    FileLog("Apply() starting.");
                    FixPacketLength();
                    RegisterHandlers();
                    _applied = true;
                    FileLog("CUO Handshake Fix applied (0xD5 challenge-response).");
                    TryLog("CUO Handshake Fix applied (0xD5 challenge-response).");
                }
                catch (Exception ex)
                {
                    FileLog("CUO Handshake Fix failed: " + ex);
                    TryLog("CUO Handshake Fix failed: " + ex);
                    // Do not rethrow — avoid breaking RE Initialize after other handlers registered.
                }
            }
        }

        private static void FixPacketLength()
        {
            Type tableType = FindType("Assistant.PacketsTable")
                ?? throw new InvalidOperationException("Assistant.PacketsTable not found — RE layout may have changed.");

            FieldInfo field = tableType.GetField("_packetsTable", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("PacketsTable._packetsTable not found — RE layout may have changed.");

            object tableObj = field.GetValue(null);
            if (!(tableObj is short[] table) || table.Length <= ChallengePacketId)
                throw new InvalidOperationException("PacketsTable._packetsTable is missing or too short.");

            short before = table[ChallengePacketId];
            table[ChallengePacketId] = ChallengePacketLength;
            FileLog($"PacketsTable[0xD5]: {before} -> {ChallengePacketLength}");
        }

        private static void RegisterHandlers()
        {
            Type handlerType = FindType("Assistant.PacketHandler")
                ?? throw new InvalidOperationException("Assistant.PacketHandler not found — RE layout may have changed.");

            Type filterCallbackType = FindType("Assistant.PacketFilterCallback")
                ?? throw new InvalidOperationException("Assistant.PacketFilterCallback not found.");
            Type viewerCallbackType = FindType("Assistant.PacketViewerCallback")
                ?? throw new InvalidOperationException("Assistant.PacketViewerCallback not found.");

            MethodInfo registerFilter = handlerType.GetMethod(
                "RegisterClientToServerFilter",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("RegisterClientToServerFilter not found.");

            MethodInfo registerViewer = handlerType.GetMethod(
                "RegisterServerToClientViewer",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("RegisterServerToClientViewer not found.");

            MethodInfo sendMethod = typeof(Bootstrap).GetMethod(
                nameof(SendChallengePacket),
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo receiveMethod = typeof(Bootstrap).GetMethod(
                nameof(ReceiveChallengePacket),
                BindingFlags.Static | BindingFlags.NonPublic);

            // CreateDelegate cannot bind (object, object) to (Packet, PacketHandlerEventArgs).
            // Emit thin adapters with the exact delegate signatures.
            Delegate sendDelegate = CreateCompatibleDelegate(filterCallbackType, sendMethod);
            Delegate receiveDelegate = CreateCompatibleDelegate(viewerCallbackType, receiveMethod);

            registerFilter.Invoke(null, new object[] { (int)ChallengePacketId, sendDelegate });
            registerViewer.Invoke(null, new object[] { (int)ChallengePacketId, receiveDelegate });
            FileLog("Registered 0xD5 filter + viewer handlers.");
        }

        private static Delegate CreateCompatibleDelegate(Type delegateType, MethodInfo target)
        {
            MethodInfo invoke = delegateType.GetMethod("Invoke")
                ?? throw new InvalidOperationException("Delegate Invoke not found: " + delegateType.FullName);

            ParameterInfo[] dps = invoke.GetParameters();
            Type[] paramTypes = new Type[dps.Length];
            for (int i = 0; i < dps.Length; i++)
                paramTypes[i] = dps[i].ParameterType;

            var dm = new DynamicMethod(
                "CuoFix_" + target.Name,
                invoke.ReturnType == typeof(void) ? typeof(void) : invoke.ReturnType,
                paramTypes,
                typeof(Bootstrap).Module,
                skipVisibility: true);

            ILGenerator il = dm.GetILGenerator();
            for (int i = 0; i < paramTypes.Length; i++)
                il.Emit(OpCodes.Ldarg, i);
            il.Emit(OpCodes.Call, target);
            il.Emit(OpCodes.Ret);

            return dm.CreateDelegate(delegateType);
        }

        private static void SendChallengePacket(object packet, object args)
        {
            PropertyInfo blockProp = args?.GetType().GetProperty("Block", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            blockProp?.SetValue(args, true);
        }

        private static void ReceiveChallengePacket(object packetReader, object args)
        {
            try
            {
                FileLog("0xD5 challenge received.");
                byte[] data = ReadPacketBytes(packetReader);
                string clientVersion = ResolveClientVersion();
                FileLog($"Computing response. len={data.Length} ver={clientVersion}");
                byte[] response = ServerCheck.ComputeResponse(data, clientVersion);
                SendToServer(response);
                FileLog("0xD5 response sent.");
            }
            catch (Exception ex)
            {
                FileLog("D5 Challenge response failed: " + ex);
                TryLog("D5 Challenge response failed: " + ex);
            }
        }

        private static byte[] ReadPacketBytes(object packetReader)
        {
            Type readerType = packetReader.GetType();

            MethodInfo seek = readerType.GetMethod(
                "Seek",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(SeekOrigin) },
                null);

            seek?.Invoke(packetReader, new object[] { 0, SeekOrigin.Begin });

            PropertyInfo lengthProp = readerType.GetProperty("Length", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("PacketReader.Length not found.");
            int length = Convert.ToInt32(lengthProp.GetValue(packetReader));

            MethodInfo readByte = readerType.GetMethod("ReadByte", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)
                ?? throw new InvalidOperationException("PacketReader.ReadByte not found.");

            byte[] data = new byte[length];
            for (int i = 0; i < length; i++)
                data[i] = Convert.ToByte(readByte.Invoke(packetReader, null));
            return data;
        }

        private static object GetClientInstance()
        {
            Type clientType = FindType("Assistant.Client")
                ?? throw new InvalidOperationException("Assistant.Client not found.");

            // RE 1.0.0.15 stores this as a static field; some builds expose a property.
            FieldInfo instanceField = clientType.GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (instanceField != null)
            {
                object fromField = instanceField.GetValue(null);
                if (fromField != null)
                    return fromField;
            }

            PropertyInfo instanceProp = clientType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object fromProp = instanceProp?.GetValue(null);
            if (fromProp != null)
                return fromProp;

            throw new InvalidOperationException("Client.Instance not found (field/property missing or null).");
        }

        private static void SendToServer(byte[] response)
        {
            object client = GetClientInstance();

            Type packetType = FindType("Assistant.Packet")
                ?? throw new InvalidOperationException("Assistant.Packet not found.");

            ConstructorInfo ctor = packetType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(byte[]), typeof(int), typeof(bool) },
                    null)
                ?? throw new InvalidOperationException("Packet(byte[], int, bool) constructor not found.");
            object packet = ctor.Invoke(new object[] { response, response.Length, false });

            MethodInfo send = null;
            Type walk = client.GetType();
            while (walk != null && send == null)
            {
                send = walk.GetMethod(
                    "SendToServer",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null,
                    new[] { packetType },
                    null);
                walk = walk.BaseType;
            }

            if (send == null)
                throw new InvalidOperationException("Client.SendToServer(Packet) not found.");

            send.Invoke(client, new[] { packet });
        }

        private static string ResolveClientVersion()
        {
            try
            {
                object client = GetClientInstance();

                string uoPath = null;
                MethodInfo getPath = client.GetType().GetMethod("GetUoFilePath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (getPath != null)
                    uoPath = getPath.Invoke(client, null) as string;

                if (string.IsNullOrEmpty(uoPath))
                {
                    // ClassicUOClient.UOFilePath is a static property in 1.0.0.15.
                    Type cuoType = FindType("Assistant.ClassicUOClient");
                    PropertyInfo staticPath = cuoType?.GetProperty("UOFilePath", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    uoPath = staticPath?.GetValue(null) as string;
                }

                if (string.IsNullOrEmpty(uoPath))
                {
                    Type walk = client.GetType();
                    while (walk != null && string.IsNullOrEmpty(uoPath))
                    {
                        PropertyInfo pathProp = walk.GetProperty("UOFilePath", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (pathProp != null)
                            uoPath = pathProp.GetValue(pathProp.GetGetMethod(true)?.IsStatic == true ? null : client) as string;
                        walk = walk.BaseType;
                    }
                }

                if (!string.IsNullOrEmpty(uoPath))
                {
                    string clientExe = Path.Combine(uoPath, "client.exe");
                    if (File.Exists(clientExe))
                    {
                        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(clientExe);
                        return string.Format("{0:00}.{1:0}.{2:0}.{3:D1}",
                            fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);
                    }

                    FileLog("UO path found but client.exe missing: " + uoPath);
                }

                MethodInfo getVer = client.GetType().GetMethod("GetClientVersion", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (getVer != null)
                {
                    string existing = getVer.Invoke(client, null) as string;
                    if (!string.IsNullOrEmpty(existing))
                        return existing;
                }
            }
            catch (Exception ex)
            {
                FileLog("ResolveClientVersion error: " + ex.Message);
            }

            return "00.0.0.0";
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType(fullName, false);
                    if (t != null)
                        return t;
                }
                catch
                {
                    // ignore
                }
            }

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type t in asm.GetTypes())
                    {
                        if (t.FullName == fullName)
                            return t;
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // ignore unloadable types
                }
            }

            return null;
        }

        private static string ResolveLogPath()
        {
            try
            {
                string dir = Path.GetDirectoryName(typeof(Bootstrap).Assembly.Location);
                if (!string.IsNullOrEmpty(dir))
                    return Path.Combine(dir, "CuoHandshakeFix.log");
            }
            catch
            {
                // ignore
            }

            return Path.Combine(Path.GetTempPath(), "CuoHandshakeFix.log");
        }

        private static void FileLog(string message)
        {
            try
            {
                string path = _logPath ?? ResolveLogPath();
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // ignore
            }
        }

        private static void TryLog(string message)
        {
            try
            {
                Type utility = FindType("Assistant.Utility");
                PropertyInfo loggerProp = utility?.GetProperty("Logger", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object logger = loggerProp?.GetValue(null);
                MethodInfo error = logger?.GetType().GetMethod("Error", new[] { typeof(string) });
                MethodInfo info = logger?.GetType().GetMethod("Info", new[] { typeof(string) })
                    ?? logger?.GetType().GetMethod("Log", new[] { typeof(string) });

                if (message.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0)
                    error?.Invoke(logger, new object[] { message });
                else
                    (info ?? error)?.Invoke(logger, new object[] { message });
            }
            catch
            {
                Debug.WriteLine(message);
            }
        }
    }
}
