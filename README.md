# DataNro

Dữ liệu vật phẩm, quái, NPC, kĩ năng và bản đồ của **Ngọc Rồng Online**, lấy thẳng từ máy chủ
game và cập nhật tự động mỗi ngày.

**Trang tra cứu:** https://duykhongphai.github.io/DataNro/

## API

Dữ liệu là tệp tĩnh, đọc bằng `raw.githubusercontent.com` hoặc bằng chính trang Pages ở trên.

```
https://raw.githubusercontent.com/duykhongphai/DataNro/main/TeaMobi/Server1/ItemTemplates.json
https://raw.githubusercontent.com/duykhongphai/DataNro/main/TeaMobi/Icons/410.png
```

Bố cục: `Nhà phát hành / Máy chủ / Loại dữ liệu`. Ảnh nằm ở `Nhà phát hành / Icons / <id>.png`
— dùng chung cho mọi máy chủ cùng nhà phát hành.

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
- Ảnh quái và ảnh nền map **không có** — chúng không lấy được qua đường này.
- Ba tệp `LinkMapsXmap.txt`, `GroupMapsXmap.txt`, `AutoLinkMapsWaypoint.txt` là đồ thị liên kết
  map, **chép tay** từ mod Dragonboy, không do máy chủ gửi nên không tự cập nhật.
- API miễn phí, đừng dùng vào việc lừa đảo hay kiếm tiền trên công sức người khác.

## Dữ liệu được lấy thế nào

[`HeadlessClient/`](HeadlessClient) là một client Ngọc Rồng **không giao diện**, viết riêng cho
việc này. Nó đăng nhập, hứng bốn bảng mẫu máy chủ gửi trong lúc bắt tay, xin ảnh icon, ghi ra
tệp rồi thoát.

Cố ý **không vào map**: máy chủ gửi đủ data / map / skill / item ngay trong lúc đăng nhập,
trước cả bước chọn nhân vật, nên tới đó là đã có mọi thứ cần xuất mà nhân vật chưa hề vào game.
Bộ đọc gói cũng chỉ hiểu đúng những mã lệnh phục vụ việc lấy dữ liệu — không đánh nhau, không
đi lại, không nhặt đồ, không nhiệm vụ.

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
| `--nhip-anh` | | Cách nhau bao lâu giữa hai lần hỏi ảnh (ms) |
| `--luot-anh` | | Tối đa bao nhiêu lượt đăng nhập lại để xin nốt ảnh |
| `--lo-anh` | | Mỗi lượt hỏi tối đa bao nhiêu id, mặc định 150 |
| `--hoi-lai-anh` | | Hỏi một id mấy lần không thấy trả lời thì bỏ, mặc định 3 |
| `--cho-dang-nhap` | | Hạn cho MỘT lần thử đăng nhập, mặc định 45000 ms |
| `--lan-dang-nhap` | | Thử đăng nhập mấy lần trước khi chịu thua, mặc định 4 |

Mã thoát: `0` xong, `1` không lấy đủ dữ liệu, `2` cấu hình sai, `3` không thấy máy chủ.

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

## Cập nhật tự động

[`.github/workflows/update-data.yml`](.github/workflows/update-data.yml) chạy 5h sáng giờ Việt
Nam, đi lần lượt mười tám máy chủ rồi commit thẳng vào repo này. Danh sách máy chủ nằm trong
biến `DANH_SACH` của workflow dưới dạng `thư mục = tên máy chủ` — **cố ý không ghi IP**, client
tự tra theo tên trong danh sách sống của game nên máy chủ đổi IP là tự theo.

Cần đúng một secret: `NRO_TAIKHOAN` = `tài khoản|mật khẩu`.

## Giấy phép

Mã nguồn phát hành theo giấy phép MIT. Dữ liệu thuộc về TeaMobi; đây chỉ là bản trích xuất để
tra cứu.
