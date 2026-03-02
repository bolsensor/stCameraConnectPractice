using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;

using Sentech.StApiDotNET;
using Sentech.GenApiDotNET;
using System.IO;



namespace YourApp

{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<CameraInfo> _devices = new ObservableCollection<CameraInfo>();
        private CameraInfo _selected; // null 가능 (C# 7.3에서는 ? 못 씀)
        private readonly CameraSdkService _sdk = new CameraSdkService();


        public MainWindow()
        {
            InitializeComponent();
            lvDevices.ItemsSource = _devices;

            txtGateway.Text = "192.168.0.1";
            txtSubnet.Text = "255.255.255.0";

            btnDisconnect.IsEnabled = false;
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _devices.Clear();
                txtStatus.Text = "검색 중...";

                CameraInfo[] cams = _sdk.DiscoverCameras();
                for (int i = 0; i < cams.Length; i++)
                    _devices.Add(cams[i]);

                txtStatus.Text = _devices.Count > 0
                    ? "검색 완료: " + _devices.Count + "대"
                    : "검색 완료: 카메라 없음";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "검색 실패";
                MessageBox.Show(ex.Message, "검색 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void lvDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = lvDevices.SelectedItem as CameraInfo;

            if (_selected == null)
            {
                txtStatus.Text = "선택된 카메라 없음";
                return;
            }

            txtStatus.Text = "선택됨: " + _selected.DisplayName + " / IP=" + _selected.Ip;
        }

        private void btnApplyIp_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show("먼저 카메라를 선택해 주세요.", "IP 적용", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IPAddress newIp;
            if (!TryParseIPv4(txtNewIp.Text.Trim(), out newIp))
            {
                MessageBox.Show("새 IP를 IPv4 형식으로 입력해 주세요. 예: 192.168.0.10",
                    "IP 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IPAddress gateway;
            if (!TryParseIPv4(txtGateway.Text.Trim(), out gateway))
            {
                MessageBox.Show("게이트웨이를 IPv4 형식으로 입력해 주세요. 예: 192.168.0.1",
                    "게이트웨이 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IPAddress subnet;
            if (!TryParseIPv4(txtSubnet.Text.Trim(), out subnet))
            {
                MessageBox.Show("서브넷을 IPv4 형식으로 입력해 주세요. 예: 255.255.255.0",
                    "서브넷 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                txtStatus.Text = "IP 적용 중...";
                _sdk.ApplyPersistentIp(_selected, newIp, subnet, gateway);

                MessageBox.Show(
                    "IP 적용 요청 완료\n대상: " + _selected.DisplayName + "\n새 IP: " + newIp,
                    "IP 적용", MessageBoxButton.OK, MessageBoxImage.Information);

                // 적용 후 재검색 권장
                btnSearch_Click(sender, e);
            }
            catch (Exception ex)
            {
                txtStatus.Text = "IP 적용 실패";
                MessageBox.Show(ex.Message, "IP 적용 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show("먼저 카메라를 선택해 주세요.", "연결", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                txtStatus.Text = "연결 중...";
                _sdk.Connect(_selected);

                txtStatus.Text = "연결 성공";
                MessageBox.Show(
                    "연결 성공\n대상: " + _selected.DisplayName + "\nIP: " + _selected.Ip,
                    "연결 성공", MessageBoxButton.OK, MessageBoxImage.Information);

                btnConnect.IsEnabled = false;
                btnDisconnect.IsEnabled = true;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "연결 실패";
                MessageBox.Show(ex.Message, "연결 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _sdk.Disconnect();
                txtStatus.Text = "연결 해제됨";
                MessageBox.Show("연결 해제 완료", "연결 해제", MessageBoxButton.OK, MessageBoxImage.Information);

                btnConnect.IsEnabled = true;
                btnDisconnect.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "연결 해제 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool TryParseIPv4(string text, out IPAddress ip)
        {
            ip = IPAddress.None;

            IPAddress parsed;
            if (!IPAddress.TryParse(text, out parsed))
                return false;

            if (parsed.AddressFamily != AddressFamily.InterNetwork)
                return false;

            ip = parsed;
            return true;
        }
    }

    public class CameraInfo
    {
        public string DisplayName { get; set; }
        public string Serial { get; set; }
        public string Mac { get; set; }
        public string Ip { get; set; }
        public string InterfaceType { get; set; }

        public IStDeviceInfo DeviceInfo { get; set; }

        public CameraInfo()
        {
            DisplayName = "";
            Serial = "";
            Mac = "";
            Ip = "";
            InterfaceType = "";
            DeviceInfo = null; // C# 7.3에서는 null 허용, 사용 시 체크
        }
    }

  public class CameraSdkService
    {
                     
        private IStSystem _system;               // 기존 Discover용
        private CStSystemArray _systems;         // 선택 연결(탐색/비교)용
        private CStSystemArray _activeSystems;   // 채택된 시스템 1개만 담은 배열(정식 연결용)

        private IStDevice _device;

        // =========================
        // Public API
        // =========================

        public CameraInfo[] DiscoverCameras()
        {
            EnsureSystem();

            List<CameraInfo> results = new List<CameraInfo>();

            _system.UpdateInterfaceList();
            uint ifCount = _system.InterfaceCount;

            for (uint i = 0; i < ifCount; i++)
            {
                IStInterface stIf = _system.GetIStInterface(i);
                stIf.UpdateDeviceList();

                uint devCount = stIf.DeviceCount;
                for (uint d = 0; d < devCount; d++)
                {
                    IStDeviceInfo devInfo = stIf.GetIStDeviceInfo(d);

                    CameraInfo cam = new CameraInfo();
                    cam.DisplayName = SafeStringGet(delegate { return devInfo.DisplayName; }, "");
                    cam.Serial = SafeStringGet(delegate { return devInfo.SerialNumber; }, "");
                    cam.InterfaceType = SafeStringGet(delegate { return devInfo.TLType; }, "");
                    cam.DeviceInfo = devInfo;

                        // 이 SDK에서는 devInfo에서 IP/MAC을 못 얻는 경우가 많으니
                        // DisplayName 안에 IP가 섞여있으면 파싱해서 채워둠(없으면 "")
                        string ipText = ExtractIPv4FromText(cam.DisplayName);
                        cam.Ip = ipText ?? "";

                        results.Add(cam);
                    }
                }
            return results.ToArray();
        }
           
       
        /// <summary>
        /// 선택한 카메라에 Persistent IP 설정
        /// (임시로 그 카메라에만 연결해서 node write 후 즉시 해제)
        /// </summary>
        public void ApplyPersistentIp(CameraInfo cam, IPAddress newIp, IPAddress subnet, IPAddress gateway)
        {
            if (cam == null)
                throw new InvalidOperationException("카메라가 선택되지 않았습니다.");

            EnsureSystemArray();

            // 임시 연결
            IStDevice tempDev = null;
            CStSystemArray tempActive = null;

            try
            {
                AcquireDeviceFor(cam, out tempDev, out tempActive);

                INodeMap nodeMap = GetRemoteNodeMap(tempDev);

                var testNode = nodeMap.GetNode<INode>("GevCurrentIPConfigurationPersistentIP");
                string typeName = testNode?.GetType().FullName;

                long ipU32 = (long)IPv4ToUInt32(newIp);
                long snU32 = (long)IPv4ToUInt32(subnet);
                long gwU32 = (long)IPv4ToUInt32(gateway);

                SetInt(nodeMap, "GevPersistentIPAddress", ipU32);
                SetInt(nodeMap, "GevPersistentSubnetMask", snU32);
                SetInt(nodeMap, "GevPersistentDefaultGateway", gwU32);

                // 옵션(있으면) Persistent 사용 enable
                TrySetBool(nodeMap, "GevCurrentIPConfigurationPersistentIP", true);
            }
            finally
            {
                (tempDev as IDisposable)?.Dispose();
                tempDev = null;

                tempActive?.Dispose();
                tempActive = null;
            }
        }

        /// <summary>
        /// 선택한 CameraInfo와 "일치하는" 실제 카메라로 정식 연결
        /// </summary>
        public void Connect(CameraInfo cam)
        {
            if (cam == null)
                throw new InvalidOperationException("카메라가 선택되지 않았습니다.");

            EnsureSystemArray();
            Disconnect(); // 기존 연결 정리

            IStDevice newDev = null;
            CStSystemArray newActive = null;

            try
            {
                AcquireDeviceFor(cam, out newDev, out newActive);

                // 성공: 정식 채택
                _device = newDev;
                _activeSystems = newActive;

                newDev = null;
                newActive = null;
            }
            finally
            {
                // 채택 실패 시에만 정리
                (newDev as IDisposable)?.Dispose();
                newActive?.Dispose();
            }
        }

        public void Disconnect()
        {
            if (_device != null)
            {
                (_device as IDisposable)?.Dispose();
                _device = null;
            }

            if (_activeSystems != null)
            {
                _activeSystems.Dispose();
                _activeSystems = null;
            }
        }

        // =========================
        // Core: 선택 매칭 로직
        // =========================

        /// <summary>
        /// cam과 일치하는 장치를 찾아서 (device + 그 device가 속한 active systemarray)를 반환
        /// </summary>
        private void AcquireDeviceFor(CameraInfo cam, out IStDevice device, out CStSystemArray activeSystems)
        {
            device = null;
            activeSystems = null;

            string targetSerial = (cam.Serial ?? "").Trim();
            uint? targetIpU32 = null;

            if (!string.IsNullOrWhiteSpace(cam.Ip))
            {
                IPAddress parsed;
                if (IPAddress.TryParse(cam.Ip, out parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
                    targetIpU32 = IPv4ToUInt32(parsed);
            }

            // 시스템(인터페이스) 하나씩 시험 연결
            for (uint i = 0; i < _systems.GetSize(); i++)
            {
                CStSystemArray trialActive = null;
                IStDevice trialDev = null;

                try
                {
                    trialActive = new CStSystemArray();
                    trialActive.Register(_systems[i]); // 시스템 1개만 담기

                    trialDev = trialActive.CreateFirstStDevice();

                    // 1) Serial 우선 비교 (가장 안정적)
                    if (!string.IsNullOrWhiteSpace(targetSerial))
                    {
                        string serial = TryGetSerial(trialDev);
                        if (!string.IsNullOrWhiteSpace(serial) &&
                            string.Equals(serial.Trim(), targetSerial, StringComparison.OrdinalIgnoreCase))
                        {
                            device = trialDev;
                            activeSystems = trialActive;
                            trialDev = null;
                            trialActive = null;
                            return;
                        }
                    }

                    // 2) IP 비교 (가능하면)
                    if (targetIpU32.HasValue)
                    {
                        uint ipU32 = TryGetCurrentIpU32(trialDev);
                        if (ipU32 == targetIpU32.Value)
                        {
                            device = trialDev;
                            activeSystems = trialActive;
                            trialDev = null;
                            trialActive = null;
                            return;
                        }
                    }

                    // 3) 마지막 보조: DisplayName 비교(정확도 낮음)
                    // 필요하면 여기에서 deviceInfo.DisplayName을 읽어서 cam.DisplayName과 비교할 수도 있음.
                }
                catch
                {
                    // 이 시스템에서 장치 없거나 열기 실패 가능 -> 다음
                }
                finally
                {
                    (trialDev as IDisposable)?.Dispose();
                    trialActive?.Dispose();
                }
            }

            throw new InvalidOperationException(
                "선택한 카메라와 일치하는 장치를 찾지 못했습니다.\n" +
                "가능 원인: Serial이 비어있음 / IP를 못 읽음 / 다른 NIC에 연결됨 / 권한/드라이버 문제");
        }

        private static string TryGetSerial(IStDevice dev)
        {
            try
            {
                // 너가 물어본 그 메서드: device -> deviceInfo 조회용(생성용 아님)
                var info = dev.GetIStDeviceInfo();
                return info != null ? info.SerialNumber : null;
            }
            catch { return null; }
        }

        private static uint TryGetCurrentIpU32(IStDevice dev)
        {
            try
            {
                INodeMap nodeMap = GetRemoteNodeMap(dev);

                // GigE Vision 노드(보통)
                long ip = GetInt(nodeMap, "GevCurrentIPAddress");
                return unchecked((uint)ip);
            }
            catch
            {
                return 0; // 못 읽으면 0 리턴
            }
        }

        // =========================
        // System 초기화
        // =========================

        private void EnsureSystem()
        {
            if (_system != null) return;
            _system = new CStSystem();
        }

        private void EnsureSystemArray()
        {
            if (_systems != null) return;
            _systems = new CStSystemArray();
        }

        // =========================
        // GenICam NodeMap helpers
        // =========================

        private static INodeMap GetRemoteNodeMap(IStDevice device)
        {
            // 네 예전 코드 스타일 유지: 가능한 경로를 try/catch로 흡수
            try
            {
                var port = device.GetRemoteIStPort();
                return port.GetINodeMap();
            }
            catch (Exception ex)
            {
                Log(ex);
            }
            //try
            //{
            //    return device.GetRemoteNodeMap();
            //}
            //catch { }

            throw new NotSupportedException("Remote NodeMap 접근 멤버를 찾지 못했습니다.");
        }

        private static void SetInt(INodeMap nodeMap, string nodeName, long value)
        {
            IInteger intNode = nodeMap.GetNode<IInteger>(nodeName);
            if (intNode == null)
                throw new ArgumentException("노드 없음: " + nodeName);

            if (!intNode.IsWritable)
                throw new InvalidOperationException("쓰기 불가: " + nodeName);

            intNode.Value = value;
        }
        private static long GetInt(INodeMap nodeMap, string nodeName)
        {
            IInteger intNode = nodeMap.GetNode<IInteger>(nodeName);
            if (intNode == null)
                throw new InvalidOperationException("IInteger 노드 없음(또는 타입 불일치): " + nodeName);

            //가능하면(지원하면) 읽기 가능 여부도 체크
            if (!intNode.IsReadable) throw new InvalidOperationException("읽기 불가: " + nodeName);

            return intNode.Value;
        }

        private static void TrySetBool(INodeMap nodeMap, string nodeName, bool value)
        {
            try
            {
                var node = nodeMap.GetNode<INode>(nodeName);  // 타입 명시

                if (node == null) return;

                // 일단 아무 것도 안 함 (타입 확인 후 구현 예정)
            }
            catch { }
        }

        // =========================
        // Util
        // =========================

        private static uint IPv4ToUInt32(IPAddress ip)
        {
            byte[] b = ip.GetAddressBytes();
            if (b.Length != 4) throw new ArgumentException("IPv4만 지원");
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        }

        private static string ExtractIPv4FromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            string[] parts = text.Split(new[] { ' ', '\t', '\r', '\n', '(', ')', '[', ']', '{', '}', ',', ';' },
                                        StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                IPAddress ip;
                if (IPAddress.TryParse(parts[i], out ip) && ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }

            return null;
        }

        private static string SafeStringGet(Func<string> getter, string fallback)
        {
            try
            {
                string v = getter();
                return v ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static void Log(Exception ex)
        {
            File.AppendAllText("error.log", ex.ToString() + Environment.NewLine);
            System.Diagnostics.Debug.WriteLine(ex.ToString());
            //Console.WriteLine(ex.ToString());
        }
    }

}
