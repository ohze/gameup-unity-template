# 📘 Cẩm Nang Convention Dành Cho Team Unity

Tài liệu này định nghĩa các quy tắc chuẩn (coding convention) giúp đội ngũ phát triển đồng bộ cách viết code, quy hoạch cấu trúc dự án thống nhất, tăng khả năng bảo trì và giảm thiểu những rủi ro conflict không đáng có trong quá trình làm việc nhóm chung trên Unity.

---

## 💻 1. Quy Tắc Code C#

### Quy tắc Đặt Tên (Naming Convention)
- **Biến `private` thông thường:** Viết theo kiểu camelCase và luôn bắt đầu bằng dấu gạch dưới `_`.
  ```csharp
  private int _playerScore;
  private float _moveSpeed;
  ```
- **Biến `[SerializeField] private`:** Để hiển thị đẹp trên Inspector Unity và làm rõ ràng cho Designer, **viết như bình thường (camelCase) và KHÔNG dùng dấu `_`**.
  ```csharp
  [SerializeField] private int maxHealth;
  [SerializeField] private GameObject bulletPrefab;
  ```
- **Biến Component (UI, Transform, GameObject...):** Sử dụng tiền tố (prefix) chỉ định loại Component ở ngay đầu tên biến thay vì dùng hậu tố (suffix). Điều này giúp IDE tự động nhóm các thành phần cùng loại khi gõ code, rất tiện lợi để tìm kiếm.
  - **Button:** `buttonPlay`, `buttonRetry` (*❌ Không dùng: `playButton`, `retryButton`*)
  - **Text / TextMeshPro:** `textTitle`, `textScore`, `textContent`
  - **Image / SpriteRenderer:** `imgBackground`, `imgIcon`, `imgAvatar`
  - **Transform:** `tfPlayer`, `tfEnemy`, `tfSpawnPoint`
  - **GameObject:** `goBullet`, `goEffect`
  - ...Các Component khác cũng áp dụng tương tự (ví dụ: `animPlayer`, `colWall`, `rbCharacter`, `sndClick`...).
  ```csharp
  // ❌ KHÔNG DÙNG HẬU TỐ (Suffix)
  [SerializeField] private Button playButton;
  [SerializeField] private TextMeshProUGUI scoreText;
  [SerializeField] private Image bgImage;

  // ✅ KHUYÊN DÙNG TIỀN TỐ (Prefix)
  [SerializeField] private Button buttonPlay;
  [SerializeField] private TextMeshProUGUI textScore;
  [SerializeField] private Image imgBackground;
  ```
- **Biến chỉ Get (Properties / Auto-properties):** Phải viết hoa chữ cái đầu tiên (PascalCase).
  ```csharp
  public int CurrentScore { get; private set; }
  public bool IsDead => _health <= 0;
  ```
- **Tên Hàm (Methods):** Bắt buộc viết hoa chữ cái đầu (PascalCase) và phải bắt đầu bằng một hành động hay động từ cụ thể.
  ```csharp
  public void CalculateDamage() { ... }
  private void SpawnEnemy() { ... }
  ```

### Quy tắc Viết Code (Coding Style)
- **Chuỗi kẹp (String Interpolation) cho việc ghép chuỗi:** Tuyệt đối **KHÔNG** sử dụng toán tử cộng (`+`) để nối chuỗi tĩnh và chuỗi động. Luôn luôn sử dụng cú pháp `$"{}"` để code gọn gàng, giảm cấp phát bộ nhớ rác và dễ đọc hơn.
  ```csharp
  // ❌ KHÔNG DÙNG
  Debug.Log("Player " + playerName + " has " + health + " HP.");

  // ✅ KHUYÊN DÙNG
  Debug.Log($"Player {playerName} has {health} HP.");
  ```
- **Hàm lồng nhau (Local Functions):** **TUYỆT ĐỐI KHÔNG viết hàm trong hàm**. Bất kì logic phụ nào cũng cần phải được trích xuất ra một hàm / method riêng (private method) đặt ngang hàng bên ngoài scope.
  ```csharp
  // ❌ KHÔNG DÙNG: Viết hàm trong hàm
  public void TakeDamage(int damage) 
  {
      void PlayHitEffect() { ... } // TUYỆT ĐỐI KHÔNG
      
      _health -= damage;
      PlayHitEffect();
  }

  // ✅ KHUYÊN DÙNG: Tách hàm rõ ràng
  public void TakeDamage(int damage) 
  {
      _health -= damage;
      PlayHitEffect();
  }
  
  private void PlayHitEffect() 
  {
      // ... Logic xử lý hiệu ứng
  }
  ```

---

## 📁 2. Tổ Chức Cấu Trúc Folder (Folder Structure)

Phân nhánh thư mục một cách rõ ràng. Gói tất cả asset của project vào một thư mục gốc riêng dành cho dự án thay vì để la liệt ở ngoài `Assets/`. Tuyệt đối không để chung code nội bộ với `Plugins` hay asset từ `ThirdParty`.

```text
Assets/
├── _Project/ (Hoặc tên riêng ví dụ: GameUpCore/)
│   ├── Art/                # Chứa 3D Models, Textures, Sprites, Materials...
│   ├── Audio/              # Chứa file SFX, BGM nhạc nền, Voice...
│   ├── Prefabs/            # Nơi lưu trữ toàn bộ các đối tượng Prefab 
│   ├── Scenes/             # Chứa tĩnh các file màn chơi .unity
│   ├── Scripts/            # Toàn bộ mã nguồn code C# của game
│   │   ├── Core/           # Chứa các Script quan trọng cốt lõi, Manager, Utilities
│   │   ├── Gameplay/       # Tương tác/Logic game (Player, Quái vật, Level...)
│   │   └── UI/             # Code xử lý giao diện UI/UX
│   └── ScriptableObjects/  # Nơi chứa các file cấu hình Data
├── Plugins/                # Chứa Plugin biên dịch gốc, các thư viện hệ thống (.dll)
└── ThirdParty/             # Các Asset/Thư viện từ Asset Store tải về (vd DOTween)
```

---

## 🗂️ 3. Tổ Chức & Lưu Trữ Scriptable Object (SO)

Rất nhiều thông số cấu hình và Dữ liệu tĩnh nên dùng ScriptableObject để giảm memory allocation.
- **Vị trí lưu trữ:** Ưu tiên lưu tập trung ở thư mục gốc kiểu `Assets/_Project/ScriptableObjects/` rồi chia nhỏ theo danh mục. Trong một số trường hợp, SO có thể đứng gần tệp Prefab hoặc logic liên quan chặt với nó.
- **Menu Tạo Mới:** Luôn sử dụng tag attribute `[CreateAssetMenu]` và tuân thủ quy tắc nhóm đường dẫn Menu có cấu trúc bậc rõ ràng để dễ chọn, tránh việc menu Create bị phình to lộn xộn.
  ```csharp
  [CreateAssetMenu(fileName = "SO_NewEnemyConfig", menuName = "GameUp/Entity Data/Enemy")]
  public class SO_EnemyConfig : ScriptableObject { ... }
  ```
- **Quy tắc Đặt Tên (Tiền tố & Hậu tố):** 
  - **Tiền tố (Prefix):** Bắt buộc phải có chữ `SO` (viết liền `SO...`) ở đầu tên class / tên file asset. Điều này giúp hệ thống và Dev nhận diện ngay đây là Scriptable Object thay vì chỉ là MonoBehaviour thông thường.
  - **Hậu tố (Suffix):** Bắt buộc gắn thêm phần đuôi để nhìn tên là biết luôn mục đích của file. Ví dụ: `Data` (chứa các chỉ số thực thể/kỹ năng), `Config` (cấu hình logic), `Settings` (cài đặt).
  - *(Ví dụ quy chuẩn: `SOGameSettings`)*.

---

## 🎭 4. Tổ Chức Scene & Prefab

Hai thành phần cực kỳ dễ gây "Merge Conflict" dữ liệu khi sáp nhập code trên SVN hay Git. Hãy tuân thủ nghiêm ngặt để bảo vệ sức khoẻ thành viên trong nhóm:
- **Nguyên lý "Prefab is King":** Đừng cố kéo thả lắp ráp logic một tính năng phức tạp trực tiếp lên một Scene. **Mọi thành phần đối tượng có cơ hội tái sử dụng phải được làm thành Prefab.**
- **Scene "Nhạt" (Empty Scene):** Một file ảnh hưởng lớn như Scene chỉ nên chứa các GameObject điểm tựa cốt lõi như Environment Setup, Static Light, Camera và các `Manager` tĩnh. Các nội dung Game và UI cần đóng gói Prefab đưa vào Scene một cách gián tiếp.
- **Khuyến Khích Prefab Variant:** Khi bạn cần tạo ra một con quái vật giống con gốc tới 90% nhưng màu lông khác, **tuyệt đối không nhấn Unpack (xé rách) Prefab**. Hãy tạo ra một **Prefab Variant** để phiên bản này kế thừa mọi thay đổi tương lai nếu có từ Prefab gốc.
- **Nested Prefab:** Phân rã giao diện khổng lồ hay một nhân vật phức tạp thành các tầng Prefab lồng nhau. Như vậy, nhiều Dev có thể cùng chỉnh sửa các phần tử khác nhau của tính năng đó cùng lúc mà không lo đụng độ file lưu lại.
