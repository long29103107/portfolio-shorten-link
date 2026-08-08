# ShortenLink Web — React frontend audit

Ngày audit: 2026-08-08  
Phạm vi: `src/ShortenLink.Web/`  
Ràng buộc: audit và đề xuất roadmap; không sửa source implementation trong lượt này.

## Kết luận ngắn

Frontend hiện tại là một hybrid feature-based/layered React app, không phải một codebase bị “sai kiến trúc”. Ranh giới `app` → `features/short-links` → `shared` đang hợp lý và đã có architecture guard. Các pure domain helper gần đây được gom vào `features/short-links/domain/` cũng là hướng đúng.

Refactor cần làm ở mức targeted, ưu tiên correctness và ownership:

1. Sửa lỗi truyền nhầm `limit/page` ở dashboard.
2. Chuẩn hóa cancellation và stale-response protection.
3. Tách hai page lớn (`ShortLinkAdminPage`, `SecurityManagementPage`) theo use case thực tế.
4. Bổ sung lint, component/integration tests và accessibility checks.
5. Chỉ sau đó mới cân nhắc split `types.ts`, global CSS hoặc auth abstraction.

Không có bằng chứng đủ mạnh để rewrite toàn bộ frontend, đổi custom router sang React Router, hoặc thêm Redux/Zustand/TanStack Query/React Hook Form một cách đại trà.

## 1. Hiện trạng thực tế

### Runtime architecture

```text
main.tsx
  └─ app/App.tsx
       ├─ custom router + history/popstate
       ├─ auth bootstrap + session events
       ├─ app shell/sidebar/navigation guard
       └─ lazy route pages
            └─ features/short-links
                 ├─ api/       transport + endpoint calls
                 ├─ hooks/     request lifecycle/stateful data logic
                 ├─ domain/    pure validation/query/presentation helpers
                 ├─ pages/     route-level composition
                 ├─ components/feature UI
                 └─ types.ts   API/UI/route types

shared/
  ├─ api/          generic transport, failures, recovery
  ├─ components/   DataTable, dialogs, pagination, toast, UI primitives
  ├─ constants/
  ├─ hooks/
  └─ lib/
```

Đây là cấu trúc theo feature có một số layer nội bộ. `scripts/check-architecture.mjs` kiểm tra được các boundary quan trọng: `shared` không import `app/features`, feature không import `app` hoặc feature khác. Guard hiện pass, nhưng chưa kiểm tra kích thước page, vị trí API/hook, aliases, test graph hay accessibility.

### Các con số đáng chú ý

| Khu vực | Hiện trạng | Nhận xét |
|---|---:|---|
| React | 19 | Đang dùng `StrictMode`, `lazy`, `Suspense`, `startTransition` |
| Build | Vite 7 + TypeScript strict | Nền tảng hiện đại, phù hợp |
| Page lớn nhất | `ShortLinkAdminPage.tsx`: 1,083 dòng | Ownership quá rộng |
| Page lớn thứ hai | `SecurityManagementPage.tsx`: 962 dòng | Hai use case users/roles bị dồn chung |
| App shell | `App.tsx`: 553 dòng | Router, auth, shell, guard và route rendering cùng file |
| Global CSS | 3,392 dòng | Chưa phải khủng hoảng, nhưng ownership khó tìm dần |
| Test | 17 file, 66 pass, 0 fail | Chủ yếu là pure/domain/API serialization |
| UI/e2e test | Chưa có | Đây là khoảng trống quan trọng |
| Lint/format | Chưa có script/config | Chưa có guard cho hooks/a11y/style |
| State/data library | Custom hooks + fetch | Hợp lý với quy mô hiện tại |
| Router | Custom history/popstate | Đủ dùng với route hiện tại, chưa cần thay ngay |

Baseline trước audit: architecture check pass, Bun tests pass, production build pass. Worktree đã có nhiều thay đổi từ các task 031 trước đó; các thay đổi đó được xem là baseline của người dùng, không phải thay đổi do audit này.

## 2. Đối chiếu với thực hành hiện đại

React khuyến nghị tránh state dư thừa/trùng lặp và tính derived data trong render; Effects nên dành cho đồng bộ với hệ thống bên ngoài, còn data fetching trong Effect phải có cleanup để tránh response cũ ghi đè response mới. Custom Hook nên đóng gói một use case stateful cụ thể, không chỉ bọc một lifecycle chung. Các nguyên tắc này được áp dụng để đánh giá code hiện tại. [React — Choosing the State Structure](https://react.dev/learn/choosing-the-state-structure), [React — You Might Not Need an Effect](https://react.dev/learn/you-might-not-need-an-effect), [React — useEffect](https://react.dev/reference/react/useEffect), [React — Reusing Logic with Custom Hooks](https://react.dev/learn/reusing-logic-with-custom-hooks)

| Vùng | Hiện tại | Common practice | Đánh giá |
|---|---|---|---|
| Boundary | `app`, `features`, `shared` rõ; có guard | Feature boundary + shared primitives | Tốt |
| API | Endpoint tập trung trong `api/`, generic transport ở `shared/api` | Một API boundary typed, không fetch rải trong UI | Tốt |
| Server state | Custom hooks, AbortController ở nhiều read flow | Query lifecycle phải có cancellation, stale protection, retry/error semantics | Đúng hướng nhưng chưa nhất quán |
| Client state | Page giữ nhiều form/dialog/selection state | State colocated theo use case, tránh page orchestrator quá lớn | Cần cải thiện |
| Effects | Có cleanup ở nhiều hooks, nhưng chưa đồng đều | Cleanup hoặc ignore stale response bắt buộc cho async Effects | Có bug cụ thể |
| Routing | Custom parser + history | Router library thường hữu ích khi route tree/nested loaders lớn | Custom router vẫn hợp lý hiện tại |
| Runtime types | TypeScript strict, nhưng nhiều `as T` từ JSON/localStorage | Validate boundary khi dữ liệu đến từ network/storage | Cần harden dần |
| Accessibility | Dialog có `role=dialog`, Escape ở `FormDialog`; menu/dialog chưa đầy đủ keyboard/focus behavior | Focus trap/restore, keyboard navigation, unique ids, automated a11y checks | Khoảng trống |
| Testing | Pure logic và API helper tests tốt | Critical user flows cần render/integration/browser coverage | Thiếu lớp kiểm chứng |
| Tooling | Vite + Bun + TS strict | Lint hooks/TS/a11y, deterministic CI scripts | Thiếu guard |
| Styling | Global CSS + class convention | Bất kỳ CSS strategy nào cũng được nếu ownership rõ | Không cần đổi công nghệ, nên chia ownership |

TypeScript đang bật `strict`, là nền tảng tốt cho static checking. Vite cũng đang được dùng đúng vai trò build/dev tool; Vite nhấn mạnh rằng biến có prefix `VITE_` được bundle ra client và không được chứa secret. [TypeScript TSConfig Reference](https://www.typescriptlang.org/tsconfig/), [Vite — Getting Started](https://vite.dev/guide/), [Vite — Env Variables and Modes](https://vite.dev/guide/env-and-mode)

## 3. Phát hiện cần xử lý

### A. Actual problems — ưu tiên P1

#### A1. Dashboard truyền nhầm `limit/page`

`listShortLinks` nhận tham số theo thứ tự `(limit, page, discovery, signal)` tại [shortLinksApi.ts](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts:103). Nhưng helper dashboard gọi `listShortLinks(1, limit, ...)` tại [useAdminDashboardData.ts](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/features/short-links/hooks/useAdminDashboardData.ts:11). Vì vậy request “recent 6 links” đang thành `limit=1, page=6`, không phải `limit=6, page=1`. Đây là lỗi correctness, không phải style issue.

Nên sửa bằng named options hoặc đổi helper để không còn positional ambiguity, sau đó thêm regression test kiểm tra query gửi lên.

#### A2. Abort bình thường bị hiển thị như lỗi timeout/network

Các hook chủ động abort request khi đổi criteria, đổi route hoặc unmount. Nhưng [http.ts](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/features/short-links/api/http.ts:40) catch mọi lỗi fetch và gọi toast; `classifyFetchFailure` lại phân loại `AbortError` thành retryable timeout tại [apiFailure.ts](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/shared/api/apiFailure.ts:66). Kết quả là thao tác điều hướng hợp lệ có thể sinh toast “server unavailable/request timed out”.

Cancellation cần là một trạng thái im lặng: không toast, không set failure state, không redirect. Chỉ timeout thực sự hoặc network failure mới đi qua recovery UI.

#### A3. Discovery imperative load có thể bị stale response ghi đè

`useShortLinkDiscovery` có AbortController cho initial/effect load, nhưng `loadLinks` được expose ra ngoài và các thao tác pagination/retry/refresh gọi không kèm signal tại [useShortLinkDiscovery.ts](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/features/short-links/hooks/useShortLinkDiscovery.ts:25). Hai lần load liên tiếp có thể hoàn thành ngược thứ tự và response cũ set lại `links`, `pageNumber`, `totalCount`.

Hook nên tự sở hữu controller hiện tại và request version; callers chỉ gọi `loadLinks(page)` hoặc các command có semantics rõ. Không nên để caller phải nhớ lifecycle cancellation.

#### A4. Share dialog thiếu stale-response protection

Effect tại [ShortLinkShareDialog.tsx](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/features/short-links/components/ShortLinkShareDialog.tsx:31) fetch theo `link`, nhưng không abort hoặc ignore response cũ. Nếu đóng/mở nhanh cho code khác, shares/mode/error của link trước có thể cập nhật dialog mới. React cũng minh họa cleanup/ignore flag là cần thiết để tránh response về sai thứ tự. [React — useEffect](https://react.dev/reference/react/useEffect)

#### A5. Auth bootstrap chưa có trạng thái “checking session”

`App` đọc token từ localStorage rồi render route ngay, trong khi `getCurrentSecurityUser()` chạy async tại [App.tsx](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/app/App.tsx:66). Điều này có thể tạo flash protected page hoặc khởi động page read với identity cũ trước khi session được xác nhận. Nên có `unknown/checking/authenticated/anonymous` state, hoặc một `AuthGate`, trước khi render protected workspace.

#### A6. Boundary dữ liệu vẫn tin tưởng JSON/localStorage bằng type assertion

API transport cast `response.json()` thành DTO; session parse localStorage cast thẳng thành `SecurityCurrentUser` tại [adminSecurity.ts](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/features/short-links/api/adminSecurity.ts:58). TypeScript không xác thực dữ liệu runtime. Nên thêm normalizer/guard ở các boundary quan trọng, đặc biệt current user, login response và các payload dùng cho permission decisions.

#### A7. API key client-side và token storage cần quyết định bảo mật rõ

`VITE_SHORTENLINK_ADMIN_API_KEY` được đọc trong client tại [adminSecurity.ts](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/features/short-links/api/adminSecurity.ts:88). Theo tài liệu Vite, mọi `VITE_*` đều được expose trong bundle; giá trị này chỉ có thể là public configuration, không được coi là secret. [Vite — Env Variables and Modes](https://vite.dev/guide/env-and-mode)

Access/refresh token cũng đang nằm trong localStorage. Đây không phải việc nên “âm thầm” đổi trong một refactor UI; cần một security decision về HttpOnly cookie/BFF hoặc chấp nhận rủi ro hiện tại trong môi trường demo.

### B. Design improvements — ưu tiên P1/P2

#### B1. `ShortLinkAdminPage` là page orchestrator quá lớn

Page có 17 state/refs trước khi tính derived state tại [ShortLinkAdminPage.tsx](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/features/short-links/pages/ShortLinkAdminPage.tsx:60), đồng thời quản lý discovery, mutation, bulk actions, edit form, share dialog, QR dialog, analytics, confirmation và table rendering.

Đề xuất target nội bộ:

```text
short-links/
  pages/ShortLinkAdminPage.tsx       # route composition
  components/admin/
    ShortLinkAdminToolbar.tsx
    ShortLinkTable.tsx
    ShortLinkEditDialog.tsx
    ShortLinkBulkActions.tsx
  hooks/useShortLinkMutations.ts
  hooks/useShortLinkSelection.ts     # chỉ nếu selection logic còn tăng
```

Tách theo use case, không tách thành hàng chục file chỉ vì file hiện tại dài.

#### B2. `SecurityManagementPage` đang chứa hai bounded use case

Page có state cho users, roles, password reset, assignment, bulk disable và role permission matrix trong cùng component; phần đầu đã có hơn 20 state tại [SecurityManagementPage.tsx](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/features/short-links/pages/SecurityManagementPage.tsx:92).

Nên tách `security/users` và `security/roles` ở mức component/hook trước; chỉ tách thành feature riêng nếu sau này có route/API ownership độc lập. Không cần đổi toàn bộ `features/short-links` ngay lập tức.

#### B3. Dialog/menu custom cần hoàn thiện accessibility

`FormDialog` đã có Escape và accessible naming, nhưng chưa focus trap/focus restore. `ConfirmDialog` dùng id tĩnh `confirm-dialog-title`/`confirm-dialog-description` tại [ConfirmDialog.tsx](D:/8.Codex/shorten-link/src/ShortenLink.Web/src/shared/components/ConfirmDialog.tsx:30); menu primitive cần kiểm tra thêm keyboard navigation và focus management. Đây là lý do nên có component tests/a11y checks trước khi tiếp tục nhân bản dialog.

#### B4. `types.ts` đang ôm nhiều loại ownership

File chứa route model, API DTO, form input, UI error types và formatter. Chưa cần split ngay, nhưng khi sửa API và UI độc lập trở nên khó review, nên tách thành `contracts.ts`, `forms.ts`, `viewModels.ts`, `formatters.ts`. Đây là maintainability improvement, không phải lỗi runtime.

#### B5. Global CSS nên chia theo ownership, không cần đổi sang Tailwind

3,392 dòng global stylesheet vẫn có variables, responsive rules và reduced-motion support. Vấn đề chính là khả năng tìm ownership, không phải CSS thuần “không hiện đại”. Có thể chia nhẹ thành `styles/base.css`, `styles/layout.css`, `styles/shared.css`, `styles/features/short-links.css` sau khi page extraction hoàn tất.

### C. Tooling/test gaps — ưu tiên P1/P2

- 17 test files hiện chủ yếu kiểm tra pure logic, query serialization, recovery và export/QR helpers; chưa có render/integration/browser test cho login, auth redirect, create/edit/delete, pagination race, dialog lifecycle.
- `tsconfig.json` chỉ include `src`, nên test files không đi qua `tsc -b`; Bun vẫn chạy được TypeScript test nhưng đây là hai lớp kiểm chứng khác nhau.
- Chưa có ESLint config/script. Nên thêm ESLint flat config với TypeScript, React Hooks và một lớp JSX accessibility phù hợp. `typescript-eslint` có quickstart chính thức cho ESLint flat config. [typescript-eslint — Getting Started](https://typescript-eslint.io/getting-started/)
- Bun test runner hỗ trợ TypeScript/JSX, UI/DOM testing, watch mode và CI integration; có thể mở rộng test stack hiện tại thay vì đổi runner ngay. [Bun — Test runner](https://bun.sh/docs/test)
- Có cả `package-lock.json` và `bun.lock`; cần chọn package manager/lockfile cho CI để tránh drift. Đây là vấn đề vận hành P2.
- Chưa có route-level error boundary. Nên thêm fallback boundary ở workspace để lỗi render một page không làm mất toàn bộ shell.

## 4. Kiến trúc đích được khuyến nghị

Giữ các boundary hiện tại và làm chúng sắc hơn:

```text
app/
  AppShell.tsx
  auth/AuthGate.tsx
  router.ts

features/short-links/
  api/                  # typed endpoint boundary
  domain/               # pure rules/normalizers/formatters
  hooks/                # concrete use cases, request lifecycle
  components/
    admin/
    security/users/
    security/roles/
  pages/                # route composition, ít state hơn
  contracts.ts          # chỉ khi types.ts bắt đầu gây coupling

shared/
  api/                  # transport/failure/recovery
  components/           # accessible primitives
  hooks/
  constants/
```

Nguyên tắc triển khai:

- API layer là nơi duy nhất biết endpoint và request DTO.
- Hook sở hữu cancellation, request version và error semantics; component không tự quản lý AbortController.
- Page compose use cases; mutation handlers không nằm chung với toàn bộ table/dialog JSX.
- Derived values tính trong render; không tạo Effect chỉ để đồng bộ state dẫn xuất.
- Chỉ dùng `useMemo`, `useCallback`, `memo` khi có evidence về cost hoặc identity contract.
- Giữ custom router cho đến khi cần nested route/layout loader/error boundary mà custom implementation bắt đầu cản trở.

## 5. Roadmap incremental

| Bước | Phạm vi | Kết quả kiểm chứng | Mức |
|---|---|---|---|
| 1 | Sửa dashboard `limit/page`; thêm test query regression | Dashboard lấy đúng trang recent và query test pass | P1 |
| 2 | Chuẩn hóa abort: transport im lặng với AbortError; discovery controller/version; share dialog cleanup | Không có false toast; stale response test pass | P1 |
| 3 | Auth gate/bootstrap state; runtime guard cho session/current-user payload | Không flash protected workspace; corrupt storage an toàn | P1 |
| 4 | Tách `ShortLinkAdminPage` thành toolbar/table/edit/mutation units | Page route còn vai trò composition; behavior không đổi | P1 |
| 5 | Tách `SecurityManagementPage` thành users/roles subtrees | Users/roles có ownership và test boundary riêng | P1 |
| 6 | Thêm ESLint flat config, hooks rules, a11y rules; chọn lockfile/CI command | Lint chạy ổn định, CI có build + test + lint | P1/P2 |
| 7 | Thêm component tests cho auth, admin list, dialog; một happy-path browser/integration suite | Cover critical flows và regression race | P1/P2 |
| 8 | Hoàn thiện dialog/menu focus behavior + route error boundary | Keyboard/a11y behavior có test | P1/P2 |
| 9 | Split `types.ts` và global CSS theo ownership nếu churn còn cao | Review/navigation dễ hơn, không đổi runtime architecture | P2 |
| 10 | Đo bundle/runtime trước khi tối ưu thêm | Có budget và evidence, không tối ưu cảm tính | P2 |

Mỗi bước nên giữ build/test/architecture check xanh và không trộn với migration công nghệ lớn. Có thể triển khai bước 1–3 trước khi đụng page decomposition; đó là nhóm có risk correctness/security cao nhất.

## 6. Những thứ không nên refactor lúc này

- Không đổi custom router sang React Router chỉ vì thư viện phổ biến hơn. Route hiện tại ít, parser rõ, và migration sẽ tạo churn lớn.
- Không thêm Redux, Zustand hoặc TanStack Query cho toàn app. Các hook hiện tại đã đủ mô hình hóa read lifecycle; chỉ cân nhắc query library nếu caching, deduplication, invalidation và polling trở thành nhu cầu thật.
- Không thêm React Hook Form/Zod hàng loạt. Form hiện tại nhỏ và validation thuần đã có; runtime schemas chỉ nên đặt ở network/storage boundaries quan trọng.
- Không rewrite global CSS sang Tailwind/CSS Modules theo preference. Chia ownership trước, đổi strategy chỉ khi có pain đo được.
- Không bọc mọi function bằng `useCallback`/`useMemo` hoặc mọi component bằng `memo`.
- Không tạo repository/service layer mới phía trên `shared/api` + feature API hiện có.
- Không tách `features/short-links` thành nhiều feature độc lập trước khi users/roles thực sự có lifecycle/ownership riêng.
- Không tiếp tục flatten/move pure domain files nếu không có import boundary hoặc ownership problem mới; phần `domain/` hiện tại đã giải quyết đúng vấn đề flatten trước đó.

## 7. Chấm điểm và verdict

| Tiêu chí | Điểm /10 | Lý do |
|---|---:|---|
| Boundary/architecture | 7.5 | Feature/shared boundary tốt, có guard |
| API/data flow | 6.5 | API tập trung, nhưng cancellation/race chưa đồng nhất và có dashboard bug |
| State/effects | 6.0 | Nhiều hook tốt, page orchestrator và một số Effect còn rủi ro |
| Type safety | 7.0 | TS strict tốt, runtime boundary còn `as T` |
| Accessibility | 5.5 | Có nền dialog semantics, thiếu focus/menu behavior và automated checks |
| Testing/tooling | 5.5 | Pure tests tốt, thiếu UI/integration/lint |
| Performance | 7.0 | Có route lazy loading, chưa có budget/measurement |
| Tổng quan | **6.4** | Có nền tảng tốt, cần refactor targeted |

### Verdict cuối

- Mức độ cần refactor: **trung bình đến khá cao**, nhưng tập trung vào correctness, async lifecycle, page ownership và quality gates.
- Phạm vi phù hợp: **incremental refactor trong cùng architecture**, không rewrite.
- Thứ tự khuyến nghị: **P1 data correctness → cancellation/auth → page decomposition → lint/UI tests → cleanup P2**.
- Sau roadmap này, target hợp lý là frontend khoảng 8/10 về maintainability mà không phải trả giá migration lớn từ router/state/CSS stack.

