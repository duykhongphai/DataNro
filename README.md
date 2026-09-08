# DataNro

Dữ liệu vật phẩm, quái, NPC, kĩ năng và bản đồ của **Ngọc Rồng Online**, lấy thẳng từ máy chủ
game và cập nhật tự động mỗi ngày.

**Trang tra cứu:** https://duykhongphai.github.io/DataNro/

## API

Dữ liệu là tệp tĩnh, đọc bằng `raw.githubusercontent.com` hoặc bằng chính trang Pages ở trên.

```
https://raw.githubusercontent.com/duykhongphai/DataNro/main/TeaMobi/Server1/ItemTemplates.json
https://raw.githubusercontent.com/duykhongphai/DataNro/main/TeaMobi/Icons/0/410.png
```

Ngoài ra:

```
TeaMobi/Mobs/0.png            tấm sprite của mẫu quái 0
TeaMobi/Mobs/MobFrames.json   bảng ô cắt + bảng khung của mọi mẫu quái
```

Bố cục: `Nhà phát hành / Máy chủ / Loại dữ liệu`. Ảnh nằm ở
`Nhà phát hành / Icons / <id chia 1000> / <id>.png` — dùng chung cho mọi máy chủ cùng nhà phát
hành. Ví dụ id 410 ở `TeaMobi/Icons/0/410.png`, id 17529 ở `TeaMobi/Icons/17/17529.png`.

Chia thư mục con vì GitHub cắt danh sách thư mục ở 1000 tệp — tệp vẫn còn đủ và raw URL vẫn
chạy, chỉ là mở trên web thì nhìn như mất bớt.

`TeaMobi/Icons/Sizes.json` là bảng kích thước của mọi ảnh, dạng `{"410":[56,56], ...}` — cần để
tính hộp bao hình NPC mà không phải đợi ba mảnh tải xong. `Icons/KhongCo.json` là danh sách id
máy chủ đã xác nhận là không có ảnh, để lượt sau khỏi hỏi lại.

Bản nén để tải về **không nằm trong repo** mà treo ở
[Release `du-lieu`](https://github.com/duykhongphai/DataNro/releases/tag/du-lieu), ghi đè mỗi
lần workflow chạy: `<nhà phát hành>-anh.zip`, `-quai.zip`, `-map.zip`, `-json.zip`. Một tệp quá
100 MB là GitHub chặn thẳng, mà mỗi ngày một bản mới cũng phình lịch sử git rất nhanh — Release
thì không tính vào đó.

`TeaMobi/Maps/MapTiles.json` là bố cục ô của từng map: `{"id":0,"w":52,"h":24,"tiles":[…]}`,
mảng `tiles` dài `w*h`, mỗi số là chỉ số ảnh trong bộ tile của map, `0` là ô trống.

### Nhiều tài khoản và chuyện lấy sạch ảnh

Hạn mức ảnh tính theo **phiên**: mỗi phiên trả lời chừng trăm gói rồi im tới hết phiên. Một tài
khoản thì chỉ còn cách ngắt ra đăng nhập lại, mỗi vòng mất gần một phút. Khai thêm tài khoản qua
secret `NRO_TK_DS` (mỗi dòng `tài khoản|mật khẩu`) thì các phiên cùng rút chung một hàng chờ,
thời gian chia đều cho số tài khoản.

Không có gói nào liệt kê kho ảnh, nên muốn lấy sạch thì phải hỏi hết từng id — đó là việc của
`NRO_ID_ANH_TOI_DA` (quét mù `0..N`). Không xong trong một hôm được, nhưng ảnh đã tải và id đã
xác nhận là không có đều được nhớ lại, nên hôm sau đi tiếp từ chỗ dở chứ không hỏi lại từ đầu.

| Máy chủ | Thư mục |
|---|---|
| Vũ trụ 1–15 | `TeaMobi/Server1` … `TeaMobi/Server15` |
| Super 1–3 | `TeaMobi/Super1` … `TeaMobi/Super3` |

`index.json` ở gốc liệt kê những máy chủ đang có dữ liệu.

### Các loại dữ liệu

| Tệp | Nội dung |
|---|---|
| `Maps.json` | id và tên map |
| `ItemTemplates.json` | vật phẩm: tên, mô tả, loại, hành tinh, sức mạnh yêu cầu, id ảnh |
| `ItemOptionTemplates.json` | loại thuộc tính của vật phẩm |
| `MobTemplates.json` | quái: tên, máu, loại, tầm đi, tốc độ |
| `NpcTemplates.json` | NPC: tên, ba part dựng hình, menu |
| `NClasses.json` | lớp nhân vật → chiêu → từng cấp (sức mạnh, sát thương, ki, hồi chiêu, giá học) |
| `SkillOptionTemplates.json` | tên các option của kĩ năng |
| `Parts.json` | mảnh dựng hình nhân vật / NPC: mỗi part một loạt khung `{id ảnh, dx, dy}` |
| `LastUpdated` | mốc cập nhật, dạng ISO 8601 |

Định dạng chép theo [DataNRO của ElectroHeavenVN][ehvn] để ai đang đọc dữ liệu của họ đổi
sang nguồn này là chạy được ngay, không phải sửa bộ đọc.

[ehvn]: https://github.com/ElectroHeavenVN/DataNRO

<details>
<summary>Ví dụ từng tệp</summary>

```jsonc
// Maps.json
{ "id": 0, "name": "Làng Aru" }

// ItemTemplates.json
{ "isUpToUp": false, "id": 26, "type": 2, "gender": 2, "level": 3, "strRequire": 50000,
  "iconID": 410, "part": -1, "name": "Găng sắt", "description": "Giúp tăng sức đánh" }

// ItemOptionTemplates.json
{ "id": 93, "type": 0, "name": "HSD # ngày" }

// MobTemplates.json
{ "mobTemplateId": 0, "rangeMove": 0, "speed": 1, "type": 0, "dartType": 25,
  "hp": 100, "name": "Mộc nhân" }

// NpcTemplates.json
{ "npcTemplateId": 0, "headId": 18, "bodyId": 19, "legId": 20,
  "name": "Ông Gôhan", "menu": [["Nói chuyện"]] }

// NClasses.json
{ "classId": 0, "name": "Trái đất", "serverName": "Chưa vào lớp",
  "skillTemplates": [ { "id": 0, "maxPoint": 7, "manaUseType": 0, "type": 1, "iconId": 539,
    "name": "Chiêu đấm Dragon", "description": "Tấn công cận chiến",
    "damInfo": "Tăng sức đánh: #%",
    "skills": [ { "point": 1, "maxFight": 1, "manaUse": 1, "skillId": 0, "dx": 32, "dy": 18,
      "damage": 100, "price": 0, "coolDown": 500, "powRequire": 1000,
      "moreInfo": "tại ông nội ngay lúc đầu" } ] } ] }
```

</details>

### Vì sao `NClasses.json` có cả `name` lẫn `serverName`

Ngọc Rồng là bản rẽ nhánh của Ninja School và **bảng tên lớp chưa đổi bao giờ**: máy chủ gửi
xuống nguyên "Chưa vào lớp / Ninja Kiếm / Ninja Phi Tiêu…". Không phải lỗi đọc gói — giải mã
tay gói kĩ năng từ đầu tới cuối thì hết sạch byte, không dư một byte nào. Client không dùng
tên đó mà lấy từ chuỗi trong máy (`mResources.MENUGENDER`).

Nên `name` là tên client hiển thị (Trái đất / Namếc / Xayda), còn `serverName` giữ nguyên tên
máy chủ gửi — cũng là chỗ duy nhất còn tên của mấy lớp thừa còn sót.

## Lưu ý

- **Không phải id ảnh nào cũng có ảnh.** Khoảng 96% id lấy được; số còn lại máy chủ thật sự
  không có — hỏi lại ba lần ở ba phiên khác nhau vẫn không thấy trả lời.
- Ảnh nền map **không có** — không lấy được qua đường này.
- Ba tệp `LinkMapsXmap.txt`, `GroupMapsXmap.txt`, `AutoLinkMapsWaypoint.txt` là đồ thị liên kết
  map, **chép tay** từ mod Dragonboy, không do máy chủ gửi nên không tự cập nhật.
- API miễn phí, đừng dùng vào việc lừa đảo hay kiếm tiền trên công sức người khác.

## Dữ liệu được lấy thế nào

[`HeadlessClient/`](HeadlessClient) là một client Ngọc Rồng **không giao diện**, viết riêng cho
việc này. Nó đăng nhập, hứng bốn bảng mẫu máy chủ gửi trong lúc bắt tay, xin ảnh icon, ghi ra
tệp rồi thoát.

Bốn bảng data / map / skill / item về ngay trong lúc bắt tay đăng nhập, trước cả bước chọn nhân
vật. Riêng **bảng mảnh dựng hình** (`Parts.json`) thì máy chủ chỉ gửi **sau khi nhân vật đã vào
map** — đo thực tế: đứng ở bước đăng nhập mà xin gói `-87` thì bốn mươi tư gói về không có lấy
một cái. Nên client vào game thật: chọn nhân vật, và **tạo nhân vật** nếu máy chủ đó chưa có.

Vào rồi thì đứng yên, không đi, không đánh, không nhặt gì. Bộ đọc gói cũng chỉ hiểu đúng những
mã lệnh phục vụ việc lấy dữ liệu.

Không muốn đụng vào game thì thêm `--khong-vao-map`: dừng ở bước đăng nhập như cũ, đổi lại
không có `Parts.json`.

### Chạy tay

```bash
dotnet run --project HeadlessClient/DataNro.HeadlessClient -- \
  --thumuc Server1 --maychu "Vũ trụ 1" --tk taikhoan --mk matkhau --ra out
```

| Tham số | Biến môi trường | Ý nghĩa |
|---|---|---|
| `--thumuc` | `NRO_THUMUC` | Tên thư mục con, ví dụ `Server1` |
| `--nph` | `NRO_NPH` | Nhà phát hành (thư mục cha), mặc định `TeaMobi` |
| `--host` / `--port` | `NRO_HOST` / `NRO_PORT` | Nối thẳng vào địa chỉ |
| `--maychu` | `NRO_MAYCHU` | Hoặc chọn theo tên trong danh sách máy chủ sống |
| `--tk` / `--mk` | `NRO_TK` / `NRO_MK` | Tài khoản |
| `--proxy` | `NRO_PROXY` | `socks5://user:pass@host:port` |
| `--ra` | `NRO_RA` | Thư mục ghi ra, mặc định `out` |
| `--khong-anh` | | Chỉ lấy JSON, bỏ qua ảnh |
| `--khong-quai` | | Bỏ qua tải sprite quái |
| `--lo-anh` / `--lo-quai` | | Mỗi lượt hỏi tối đa bao nhiêu cái |
| `--khong-vao-map` | | Không vào game (mất `Parts.json`) |
| `--cho-part` | | Chờ bảng part bao lâu sau khi vào map, mặc định 60000 ms |
| `--nhip-anh` | | Cách nhau bao lâu giữa hai lần hỏi ảnh (ms) |
| `--luot-anh` | | Tối đa bao nhiêu lượt đăng nhập lại để xin nốt ảnh |
| `--lo-anh` | | Mỗi lượt hỏi tối đa bao nhiêu id, mặc định 150 |
| `--hoi-lai-anh` | | Hỏi một id mấy lần không thấy trả lời thì bỏ, mặc định 3 |
| `--cho-dang-nhap` | | Hạn cho MỘT lần thử đăng nhập, mặc định 45000 ms |
| `--lan-dang-nhap` | | Thử đăng nhập mấy lần trước khi chịu thua, mặc định 4 |

Mã thoát: `0` xong, `1` không lấy đủ dữ liệu, `2` cấu hình sai, `3` không thấy máy chủ.

### Vẽ NPC và quái

Đây là chỗ dễ hiểu sai nhất, nên nói rõ.

**NPC ghép từ ba mảnh.** `headId` / `bodyId` / `legId` **không phải id ảnh** mà là chỉ số vào
`Parts.json`. Mỗi part là một loạt khung `{id ảnh, dx, dy}`, và bảng tư thế của client chọn
khung nào cùng độ lệch. Công thức (Npc.cs của bản giải nén):

```
vẽ đầu, rồi chân, rồi thân — thân sau cùng nên đè lên trên
x = tuThe[k][1] + khung.dx
y = -tuThe[k][2] + khung.dy        (dấu TRỪ ở dy của tư thế)
```

Thứ tự mục trong bảng tư thế là **đầu / chân / thân**, còn ba id của NPC là **đầu / thân /
chân** — lệch nhau nên rất dễ ghép nhầm. Bảng tư thế là hằng nằm trong client (33 tư thế),
`index.html` chép sẵn; NPC đứng yên dùng tư thế 0.

Vài NPC client **không** ghép từ part, id nằm cứng trong `Npc.paint`:

| NPC | Client vẽ gì |
|---|---|
| `3` Rương đồ | ảnh `265` |
| `6` Khu vực | ảnh `545` |
| `4` Đậu thần | không vẽ ở đây — cây đậu có hoạt ảnh riêng |
| `50` Quả trứng, `51` Dưa hấu | mảng ảnh máy chủ gửi kèm lúc chúng hiện trong map |

Ba part của bốn con cuối đều là `-1`, nên có cố cũng không ghép được gì. Ghép part cho NPC `3`
thì ra hai mảnh rời chẳng ra hình gì — đó là dấu hiệu đã bỏ sót bảng ngoại lệ này.

NPC **không cùng cỡ**: Quốc Vương hay Rồng Thiêng cao gấp mấy lần người thường, nên khung nhìn
phải tính theo hộp bao thật của ba mảnh chứ không đóng cứng một ô.

**Quái thì mỗi con một tấm sprite riêng**, không ghép từ part. `MobFrames.json` cho biết cắt
tấm ấy ở đâu:

```jsonc
{ "mobTemplateId": 0, "width": 24, "height": 32,
  "scale": 4, "sheetW": 204, "sheetH": 160,                     // tấm png: cỡ thật + mức phóng
  "rects":  [{ "id": 0, "x": 0, "y": 0, "w": 24, "h": 32 }],   // ô cắt, tính bằng đơn vị game
  "frames": [[{ "dx": 0, "dy": 0, "o": 0 }]],                   // mỗi khung là mấy mảnh
  "anim":   [0, 1, 2] }                                         // chuỗi hoạt ảnh
```

**Mọi con số toạ độ đều là đơn vị game**, kể cả bảng ô — muốn cắt trên tấm png thì nhân với
`scale`. Client nhân cả toạ độ đích lẫn ô cắt với mức phóng (`mGraphics.drawRegion`), ta xin
ảnh ở mức 4 nên `scale` gần như luôn là 4. Quên chỗ này thì hình ra đúng hình dạng nhưng các
mảnh rời nhau ra.

Ba chỗ lệch chuẩn, đều nằm ở mấy con boss:

- **Đuôi hoạt ảnh có con đếm bằng một byte** thay vì `short` (Godzilla, Kong). Client đọc bằng
  `readShort` rồi bọc cả hàm trong `catch` rỗng, nên nó lặng lẽ **bỏ luôn hoạt ảnh** của những
  con này. Ở đây thì thử cách đọc nào ăn khớp trọn vẹn số byte còn lại.
- **`"scale": 1`** — vài con (Hirudegarn `70`, Vua Bạch Tuộc `71`) máy chủ gửi thẳng **ảnh gốc
  chưa phóng**, không có trường nào báo trước. Tự đoán bằng cách so kích thước tấm png với vùng
  mà bảng ô phủ tới, nên `sheetW`/`sheetH` có sẵn trong tệp để bên vẽ khỏi phải chờ ảnh tải
  xong mới biết.
- **`"autoSize": true`** nghĩa là máy chủ gửi tệp nguồn dạng chữ (`==== SMALLIMAGES ====` /
  `FRAMES` / `SEQUENCE`) chứ không phải gói nhị phân — Hirudegarn là một ví dụ. Bảng ô trong đó
  **chỉ có toạ độ góc**, rộng/cao là do công cụ này tự dò từ vùng đục của tấm PNG nên có thể
  lệch vài điểm ảnh. Client gặp định dạng này thì tràn mảng ngay từ byte đầu và không vẽ được
  gì cả.

Bảng ô thi thoảng có **ô rác thò hẳn ra ngoài tấm ảnh** (con `71` có ô ở `y = 255` trong khi
tấm chỉ cao 124). Client cũng gặp, và nó bọc `drawRegion` trong `catch` rỗng nên chỉ là không
vẽ ra gì.

Bốn mẫu quái (`28`, `29`, `30`, `85`) thì máy chủ không trả lời gói xin hình, nên không có
tấm sprite nào cả.

### Bố cục map

`Maps.json` chỉ có id với tên — DataNRO của ElectroHeavenVN cũng vậy, họ không lấy phần vẽ map.
Bố cục ô thì client nạp từ tệp `/mymap/<id>` đóng gói sẵn, mà bản giải nén chỉ có bảy map; thiếu
thì nó gọi `Service.requestMaptemplate` — **gói `-28` nhánh 10**, gửi lên một byte id, máy chủ
trả về `byte rộng, byte cao` rồi `rộng*cao` byte chỉ số ô. Hỏi thẳng thế này thì không cần đi
vào từng map. Lấy được 185/187.

Chưa ghép được ảnh nền thật vì còn thiếu `tileID` — số hiệu bộ tile — thứ máy chủ chỉ gửi kèm
gói vào map (`-24`); ảnh tile thì nằm trong tài nguyên client (`res/x4/t/<tileID>/`) chứ không
xin qua gói ảnh được. Trang web hiện vẽ sơ đồ ô, đủ thấy hình dáng map và nền đất nằm đâu.

### Ảnh: máy chủ chỉ cho khoảng một trăm mỗi phiên

Không có gói nào xin được cả kho ảnh — chỉ có gói hỏi từng id một, và máy chủ **im lặng** với
id nó không có.

Đo thực tế: máy chủ trả lời khoảng **một trăm** gói mỗi phiên rồi **im hẳn** cho tới hết phiên,
dù kết nối vẫn sống. Là hạn theo *số lần hỏi mỗi phiên* chứ không phải theo nhịp, nên hạ nhịp
không cứu được — phải ngắt ra rồi đăng nhập lại.

Chỗ tinh tế là phân biệt "không có ảnh" với "máy chủ đã ngừng trả lời", vì **cả hai đều là im
lặng**. Không suy đoán được, nên luật ở đây là:

1. Chỉ id nào máy chủ **thật sự trả lời** mới rời hàng chờ — gói trả lời có mang id nên biết
   chính xác cái nào.
2. Id im lặng xuống **cuối** hàng chờ kèm bộ đếm. Xuống cuối chứ không nằm lại đầu: id không có
   ảnh thì im mãi mãi, để chúng ở đầu là mỗi lượt lại hỏi đúng chúng, dồn dần cho tới khi chiếm
   hết cả lô và không id mới nào được hỏi nữa.
3. Hỏi đủ **ba lần ở ba phiên khác nhau** mà vẫn im thì mới kết luận là không có ảnh.

Vòng lặp chỉ dừng khi hàng chờ rỗng. Dừng vì hết giờ hay mất kết nối thì nó nói thẳng
*"CHƯA hỏi hết"* để lần chạy sau xin tiếp.

Từng suy đoán theo mốc thời gian — *"id nào hỏi trước lần trả lời cuối cùng thì coi như xong"* —
và sai nặng: ta hỏi mỗi 40ms còn trả lời thì về trễ, nên tới lúc gói cuối rơi xuống đã hỏi thêm
hai ba trăm id nữa, cả đám bị gạch oan. Một lượt chạy ra 597/1895 ảnh rồi tưởng là xong.

Ảnh part của NPC hoá ra nằm **chung bảng** với icon vật phẩm, nên hỏi part là ra luôn hình NPC.

Ảnh xin ở **mức phóng 4** (trường thứ hai của gói `CLIENT_INFO`, tức `mGraphics.zoomLevel` bên
client) nên nét gấp bốn lần cỡ trong game: icon id 3 ra `80×28` thay vì `20×7`.

## Clone mà không kéo dữ liệu về máy

Thư mục `TeaMobi/` nặng hơn 20 MB (2035 tệp) mà sửa mã nguồn thì chẳng cần tới. Bảo git giữ
nó trong repo nhưng đừng bung ra đĩa:

```bash
git sparse-checkout set --no-cone '/*' '!/TeaMobi'
```

Cây làm việc còn dưới 1 MB, `git status` vẫn sạch, `git pull` / `git push` vẫn bình thường.
Muốn lấy lại thì `git sparse-checkout disable`.

Trên Windows nhớ chạy lệnh này bằng **PowerShell**, đừng dùng Git Bash: nó tưởng `/TeaMobi` là
đường dẫn tuyệt đối rồi đổi thành `C:/Program Files/Git/TeaMobi`, mẫu thành vô nghĩa mà không
báo lỗi gì.

Hai điều cần nhớ khi đang bật sparse-checkout:

- **Đừng chạy client với `--ra .`** — nó ghi vào `TeaMobi/`, mà git đang đánh dấu bỏ qua những
  đường dẫn đó nên `git status` không thấy và `git add` không nhặt. Chạy tay thì dùng `--ra out`.
- Mở `index.html` ở máy sẽ không có dữ liệu để đọc; xem trang thật ở link Pages.

Lệnh trên chỉ gọn **cây làm việc**; thư mục `.git` vẫn giữ đủ lịch sử (~64 MB). Muốn gọn cả cái
đó thì clone lại theo kiểu tải blob khi cần:

```bash
git clone --filter=blob:none --sparse https://github.com/duykhongphai/DataNro.git
```

## Cập nhật tự động

[`.github/workflows/update-data.yml`](.github/workflows/update-data.yml) chạy 5h sáng giờ Việt
Nam, đi lần lượt mười tám máy chủ rồi commit thẳng vào repo này. Danh sách máy chủ nằm trong
biến `DANH_SACH` của workflow dưới dạng `thư mục = tên máy chủ` — **cố ý không ghi IP**, client
tự tra theo tên trong danh sách sống của game nên máy chủ đổi IP là tự theo.

Cần đúng một secret: `NRO_TAIKHOAN` = `tài khoản|mật khẩu`.

## Giấy phép

Mã nguồn phát hành theo giấy phép MIT. Dữ liệu thuộc về TeaMobi; đây chỉ là bản trích xuất để
tra cứu.
