# 🏛️ MATERIAL CONTROL – FULLSTACK ARCHITECTURE & BOILERPLATE GUIDE
> **Tài liệu đặc tả kiến trúc toàn diện và khung mẫu (Boilerplate) cho Backend (.NET DDD + ABP) & Frontend (React TypeScript).**  
> Dùng làm cẩm nang chuẩn (Context Prompt) cho AI để tái tạo, sinh khung dự án hoặc phát triển đồ án tốt nghiệp / dự án doanh nghiệp quy mô lớn.

---

# MỤC LỤC
1. [Tổng quan Kiến trúc & Tech Stack](#1-tổng-quan-kiến-trúc--tech-stack)
2. [Cấu trúc Thư mục Chuẩn (BE & FE)](#2-cấu-trúc-thư-mục-chuẩn-be--fe)
3. [Backend Core Abstractions & Base Files](#3-backend-core-abstractions--base-files)
   - 3.1 BaseEntity & Filter Attributes
   - 3.2 Chuẩn Request & Response (BaseFilterQuery, BaseResponse, PaginatedResult)
   - 3.3 Base EF Core Configuration (EntityBaseConfiguration)
   - 3.4 Base Repository (IMesRepository & MesRepository)
   - 3.5 Base Application Service (SevagoMaterialControlAppService)
   - 3.6 Base Endpoint Handler (Minimal API & IEndpointBase)
4. [Frontend Core Abstractions & Base Files](#4-frontend-core-abstractions--base-files)
   - 4.1 Base API Interfaces (PageOptionsDto, ResListResponse)
   - 4.2 Axios Instance & Cơ chế Refresh Token tự động (Status 777)
   - 4.3 Utilities bóc tách API & Xử lý lỗi (unwrapBaseResponse, getApiErrorMessage)
   - 4.4 Dynamic Column Distinct Fetcher (Lọc cột phía Server)
   - 4.5 Base Hooks (useCustomSearchParams, useServerColumnFilters)
   - 4.6 Base Table Component (TableLayered)
   - 4.7 Redux Store & Router Architecture
5. [End-to-End Feature Blueprint (Full Flow mẫu từ BE sang FE)](#5-end-to-end-feature-blueprint-full-flow-mẫu-từ-be-sang-fe)
6. [Quy tắc vàng & Kinh nghiệm thực chiến (Best Practices & Gotchas)](#6-quy-tắc-vàng--kinh-nghiệm-thực-chiến-best-practices--gotchas)

---

# 1. TỔNG QUAN KIẾN TRÚC & TECH STACK

Hệ thống được thiết kế theo tư duy **Microservices-ready**, sử dụng **Clean Architecture** kết hợp với **Domain-Driven Design (DDD)** ở Backend và **Feature-driven Component-based Architecture** ở Frontend.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        FRONTEND (React 19 + Vite)                      │
│   Screens & Parts  ──►  Custom Hooks  ──►  Axios Client (Multi-Host)  │
│   (TableLayered)        (URL Params)       (Auto Refresh Token 777)    │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ HTTP / REST API (JSON)
┌───────────────────────────────────▼────────────────────────────────────┐
│                        BACKEND (.NET 8/9 C#)                           │
│   HttpApi (Minimal API)         ──► IEndpointBase                      │
│   Application.Contracts         ──► CQRS (Commands, Queries, Results)  │
│   Application                   ──► SevagoMaterialControlAppService    │
│   Domain                        ──► BaseEntity, IMesRepository         │
│   EntityFrameworkCore           ──► MesRepository, Npgsql PostgreSQL   │
└────────────────────────────────────────────────────────────────────────┘
```

### Backend Stack:
* **Framework:** .NET 8/9 C#, **ABP Framework** (`Volo.Abp`), ASP.NET Core Minimal APIs.
* **Database & ORM:** PostgreSQL, **EF Core**, `Npgsql.EntityFrameworkCore.PostgreSQL`.
* **Arch Style:** Clean Architecture, DDD, CQRS-lite, Repository Pattern với dynamic projection.
* **Message Broker & Background Tasks:** Kafka (`Confluent.Kafka`), ABP Background Workers.
* **Authentication & Authorization:** JWT Bearer, SSO OIDC/OAuth2 PKCE, Permission Attributes.

### Frontend Stack:
* **Core:** React 19, TypeScript, Vite.
* **UI & Styling:** MUI (Material UI) v7, Emotion, `sevago-library` (Design System nội bộ).
* **State Management:** Redux Toolkit (`@reduxjs/toolkit`), `redux-persist`.
* **Routing:** `react-router-dom` v7 (Protected routes, Dynamic sidebar, Permission Gate).
* **Forms & Validation:** Formik, Yup.
* **Networking:** Axios (Multi-instance, Request/Response Interceptors, Auto token refresh).

---

# 2. CẤU TRÚC THƯ MỤC CHUẨN (BE & FE)

### 2.1 Cấu trúc Backend (`material-control-be`)
```
material-control-be/
├── SEVAGO.MaterialControl.HttpApi.Host/       # Host Web API, Program.cs, DI Container
├── SEVAGO.MaterialControl.Background.Host/   # Host chạy Worker ngầm, Kafka Consumers
├── src/
│   ├── Core/                                 # THƯ VIỆN BASE NỀN TẢNG
│   │   ├── SEVAGO.MaterialControl.Core/      # MesRepository, Endpoint base
│   │   ├── SEVAGO.MaterialControl.Core.Domain/       # BaseEntity
│   │   └── SEVAGO.MaterialControl.Core.Domain.Shared/ # BaseResponse, PaginatedResult, BaseFilterQuery
│   ├── SEVAGO.MaterialControl.Domain.Shared/ # Constants, Enums, Error Codes
│   ├── SEVAGO.MaterialControl.Domain/        # Entities, Repository Interfaces, Domain Services
│   │   └── Entities/
│   │       └── <Feature>/                    # Entity, Interface Repo
│   ├── SEVAGO.MaterialControl.Application.Contracts/ # Giao tiếp CQRS
│   │   └── Features/<Feature>Contracts/
│   │       ├── Commands/                     # DTO tạo / sửa / xóa
│   │       ├── Queries/                      # DTO lọc (kế thừa BaseFilterQuery)
│   │       ├── Results/                      # DTO đầu ra
│   │       └── Services/                     # I<Feature>AppService
│   ├── SEVAGO.MaterialControl.Application/   # Xử lý Logic
│   │   └── Features/<Feature>Application/
│   │       ├── Services/                     # <Feature>AppService : SevagoMaterialControlAppService
│   │       └── Mappings/                     # AutoMapper Profile
│   ├── SEVAGO.MaterialControl.EntityFrameworkCore/   # Tầng dữ liệu PostgreSQL
│   │   ├── BuilderConfiguration/             # EntityTypeConfiguration
│   │   ├── EntityFrameworkCore/              # DbContext (Main & Read-replica)
│   │   └── Repositories/                     # <Feature>Repository : MesRepository
│   └── SEVAGO.MaterialControl.HttpApi/       # Controller / Minimal API
│       └── Endpoints/<Feature>/              # Handler : IEndpointBase
```

### 2.2 Cấu trúc Frontend (`material-control-admin`)
```
material-control-admin/
├── src/
│   ├── apis/                                 # Tầng kết nối BE
│   │   └── <feature>/
│   │       ├── <feature>.api.ts              # Export các hàm gọi axiosRequest
│   │       ├── <feature>.entities.ts         # Type Entity trả về từ BE
│   │       ├── <feature>.interface.ts        # Type Params, Query, Body
│   │       └── <feature>.enum.ts             # Enums dùng cho API
│   ├── common/                               # Hạ tầng & Thư viện dùng chung
│   │   ├── configs/                          # axios.config.ts, socket.config.ts
│   │   ├── interfaces/                       # api.interface.ts (BaseEntity, PageOptionsDto)
│   │   └── utils/                            # api-base-response.util.ts, column-distinct-fetcher.util.ts
│   ├── components/                           # Reusable UI Primitives
│   │   ├── table/                            # TableLayered, TableCollapse
│   │   ├── filter-dropdown/                  # Server-side column filtering
│   │   └── permission-gate/                  # Chặn nút bấm / render theo quyền
│   ├── hooks/                                # Custom Hooks
│   │   ├── use-custom-search-params.hook.tsx # Sync URL <-> Filter state
│   │   └── use-server-column-filters.ts      # Quản lý bộ lọc cột động
│   ├── redux/                                # Quản lý State toàn cục
│   │   ├── account/                          # User profile, tokens, permissions
│   │   ├── system/                           # Mode Dark/Light, Modal cảnh báo hết phiên
│   │   └── store.redux.ts                    # Store, Redux-persist & Migration
│   ├── router/                               # Điều hướng & Phân quyền
│   │   ├── route.constant.ts                 # Toàn bộ danh mục menu, route, quyền
│   │   └── render.route.tsx                  # Render <Route> động
│   └── screens/                              # Giao diện chức năng
│       └── <feature>/
│           ├── <feature>.screen.tsx          # Màn hình chính
│           ├── <feature>.interface.ts        # State & Props của màn hình
│           ├── <feature>.constant.ts         # Cấu hình Columns bảng, Default Params
│           ├── <feature>-column-filters.config.ts # Map tên cột FE sang tên cột BE
│           └── parts/                        # Component con (Bảng, Thanh lọc, Dialog)
```

---

# 3. BACKEND CORE ABSTRACTIONS & BASE FILES

## 3.1 `BaseEntity` – Gốc của mọi bảng Database
Mọi Entity trong hệ thống **BẮT BUỘC** kế thừa `BaseEntity`. Không dùng `DateTime` mà dùng **Unix Timestamp (`long` tính bằng giây)** để đảm bảo đồng nhất múi giờ quốc tế.

```csharp
// Path: src/Core/SEVAGO.MaterialControl.Core.Domain/Common/BaseEntity.cs
using NpgsqlTypes;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

public abstract class BaseEntity : Entity<Guid>, IModifiable, ICreatable, IDeletable, ISearchable, ISoftDelete, IAuditUserSnapshot
{
    public BaseEntity()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        CreatedOn = now;
        ModifiedOn = now;
    }

    public string CreatedBy { get; set; }
    public long CreatedOn { get; set; }         // Unix timestamp (seconds)
    public string ModifiedBy { get; set; }
    public long ModifiedOn { get; set; }        // Unix timestamp (seconds)
    public bool IsDeleted { get; set; } = false;// Soft Delete flag
    public string DeletedBy { get; set; }
    public long? DeletedOn { get; set; }

    public NpgsqlTsVector SearchVector { get; set; } // Hỗ trợ PostgreSQL Full-Text Search (GIN Index)

    [Column(TypeName = "jsonb")]
    public string Created { get; set; }         // Audit snapshot JSON lúc tạo { id, name, code }

    [Column(TypeName = "jsonb")]
    public string Modified { get; set; }        // Audit snapshot JSON lúc sửa

    public void AssignIdIfEmptyForBulkInsert()
    {
        if (Id == Guid.Empty) Id = Guid.NewGuid();
    }
}
```

### Các Attributes đi kèm Entity:
* `[SevagoTable("table_name", Schema = "material_control_develop")]`: Định danh bảng và schema PostgreSQL.
* `[Filterable(ColumnVariant.Text)]`: Cho phép MesRepository tự động filter cột text.
* `[Filterable(ColumnVariant.DateRange, UnixUnit = UnixTimestampUnit.Seconds)]`: Lọc khoảng ngày theo Unix Timestamp.
* `[Filterable(ColumnVariant.MultiSelect)]`: Lọc danh sách enum hoặc GUID.

---

## 3.2 Chuẩn Request & Response

### A. `BaseFilterQuery` – Chuẩn Request tìm kiếm, lọc và phân trang
Mọi class Query nhận từ client gửi lên để xem danh sách đều phải kế thừa `BaseFilterQuery`:

```csharp
// Path: src/Core/SEVAGO.MaterialControl.Core.Domain.Shared/Bases/BaseQueryParameters.cs
public class BaseFilterQuery
{
    public int Page { get; set; } = 0;
    public int Take { get; set; } = 100;
    public SortOption[] SortBy { get; set; } = [];
    public string[] SelectFields { get; set; } = [];
    public long FromDate { get; set; }    // Lọc từ ngày (Unix seconds)
    public long ToDate { get; set; }      // Lọc đến ngày (Unix seconds)
    public string Keyword { get; set; }   // Tìm kiếm toàn văn
    public Dictionary<string, string> Filters { get; set; } = [];
    public List<ColumnFilterDto> ColumnFilters { get; set; } = []; // Bộ lọc từng cột từ Header bảng FE
}

public class ColumnFilterDto
{
    public string Field { get; set; }
    public string DataType { get; set; }
    public List<string> SelectedValues { get; set; } = [];
    public ColumnFilterConditionDto Condition { get; set; }
}

public class SortOption
{
    public string Field { get; set; }
    public bool IsDescending { get; set; }
}
```

### B. `BaseResponse<T>` & `PaginatedResult<T>` – Chuẩn phản hồi từ Server
```csharp
// Path: src/Core/SEVAGO.MaterialControl.Core.Domain.Shared/Bases/BaseResponse.cs
public class BaseResponse
{
    public bool Succeeded { get; set; }
    public string Message { get; set; }
    public Dictionary<string, string[]> Errors { get; set; } = [];
    public HttpStatusCode StatusCode { get; set; }
}

public class BaseResponse<T> : BaseResponse
{
    public object Meta { get; set; }
    public T Data { get; set; }
}

// Path: src/Core/SEVAGO.MaterialControl.Core.Domain.Shared/Bases/PaginatedResult.cs
public class PaginatedResult<T>
{
    public IEnumerable<T> List { get; set; } = [];
    public HttpStatusCode StatusCode { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public long Total { get; set; }
    public int PageSize { get; set; }
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
    public string Message { get; set; }
    public bool Succeeded { get; set; }
}
```

---

## 3.3 Base EF Core Configuration (`EntityBaseConfiguration<T>`)
Tự động thiết lập Primary Key, SeqGuid Generator của PostgreSQL, GIN index cho Search Vector và Unix default epoch.

```csharp
// Path: src/SEVAGO.MaterialControl.EntityFrameworkCore/BuilderConfiguration/BaseCommandEntityTypeConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.ValueGeneration;

public class EntityBaseConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasValueGenerator<NpgsqlSequentialGuidValueGenerator>();

        builder.HasIndex(d => d.SearchVector).HasMethod("GIN");

        builder.Property(x => x.CreatedOn)
               .HasColumnType("bigint")
               .HasDefaultValueSql("EXTRACT(EPOCH FROM now())::bigint");

        builder.Property(x => x.ModifiedOn)
               .HasColumnType("bigint")
               .HasDefaultValueSql("EXTRACT(EPOCH FROM now())::bigint");

        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.Created).HasColumnType("jsonb");
        builder.Property(x => x.Modified).HasColumnType("jsonb");
    }
}
```

---

## 3.4 Base Repository (`IMesRepository` & `MesRepository`)
Xương sống của tầng Data Access. Cung cấp sẵn cơ chế tự phân tích `[Filterable]` attribute để lọc tự động, phân trang, sort và gom distinct values.

```csharp
// Path: src/Core/SEVAGO.MaterialControl.Core/DataAccess/Repository/IMesRepository.cs
public interface IMesRepository<TEntity, TKey> : IEfCoreRepository<TEntity, TKey> where TEntity : class, IEntity<TKey>
{
    Task<(List<TDTO> data, long totalCount)> FilterProjectedDataAsync<TDTO>(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> filterFunc,
        BaseFilterQuery parameters,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> defaultOrder = null
    ) where TDTO : class;

    Task<List<ColumnFilterDistinctValueDto>> GetColumnDistinctValuesAsync(
        string fieldName,
        BaseFilterQuery parameters,
        Func<IQueryable<TEntity>, IQueryable<TEntity>> baseFilter = null,
        int maxValues = 200
    );
}
```

---

## 3.5 Base Application Service (`SevagoMaterialControlAppService`)
Kế thừa `BaseResponseHandler` và ABP `ApplicationService`. Cung cấp sẵn AutoMapper `ObjectMapper` và các hàm Helper trả response.

```csharp
// Path: src/SEVAGO.MaterialControl.Application/SevagoMaterialControlAppService.cs
public abstract class SevagoMaterialControlAppService : BaseResponseHandler
{
    protected SevagoMaterialControlAppService()
    {
        LocalizationResource = typeof(SevagoMaterialControlResource);
        ObjectMapperContext = typeof(SevagoMaterialControlApplicationModule);
    }
}

// Các helper có sẵn trong BaseResponseHandler:
// return Success(data);
// return Success(list, totalCount, page, pageSize);
// return BadRequest<T>("Thông báo lỗi!");
// return NotFound<T>("Không tìm thấy dữ liệu!");
```

---

## 3.6 Base Endpoint Handler (Minimal API & `IEndpointBase`)
Tất cả endpoint dùng cơ chế Minimal API của .NET kết hợp ABP DI:

```csharp
// Path: src/Core/SEVAGO.MaterialControl.Core/Endpoint/IEndpointBase.cs
public interface IEndpointBase : ITransientDependency
{
    void MapEndpoint(IEndpointRouteBuilder app);
}

// Path: src/Core/SEVAGO.MaterialControl.Core/Endpoint/Extention.cs
public static class ResultExtensions
{
    public static IResult CustomResult<T>(this BaseResponse<T> response) =>
        response.StatusCode switch
        {
            HttpStatusCode.OK => Results.Ok(response),
            HttpStatusCode.BadRequest => Results.BadRequest(response),
            HttpStatusCode.NotFound => Results.NotFound(response),
            _ => Results.BadRequest(response)
        };

    public static IResult CustomResult<T>(this PaginatedResult<T> response) =>
        response.StatusCode switch
        {
            HttpStatusCode.OK => Results.Ok(response),
            _ => Results.BadRequest(response)
        };
}
```

---

# 4. FRONTEND CORE ABSTRACTIONS & BASE FILES

## 4.1 Base API Interfaces (`api.interface.ts`)
Tương thích hoàn toàn với các class Base của Backend:

```typescript
// Path: src/common/interfaces/api.interface.ts
export interface BaseEntity {
  id: string;
  createdAt: Date;
  updatedAt: Date;
  createdBy?: Partial<UserSnapshot>;
  updatedBy?: Partial<UserSnapshot>;
}

export interface PageOptionsDto {
  page: number;
  take: number;
  search?: string;
  keyword?: string;
  sortBy?: { field: string; isDescending: boolean }[];
  columnFilters?: ColumnFilterDto[];
  fromDate?: number;
  toDate?: number;
}

export interface ResListResponse<T> {
  list: T[];
  statusCode: number;
  currentPage: number;
  totalPages: number;
  total: number;
  pageSize: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
  succeeded: boolean;
}

export interface UserSnapshot {
  id: string;
  name: string;
  code: string;
  url: string;
}
```

---

## 4.2 Axios Instance & Cơ chế Refresh Token tự động (Status 777)
Xử lý multi-service host và cơ chế tự động refresh token mượt mà: khi server trả về HTTP Status `777` (Mã refresh nội bộ), hệ thống tự đóng băng request, gọi API cấp lại `accessToken` và thử lại request cũ:

```typescript
// Path: src/common/configs/axios.config.ts
import axios, { AxiosInstance } from 'axios';
import { HttpStatusSpecial } from 'sevago-library';

export const createAxiosRequest = (baseURL: string): AxiosInstance => {
  const instance = axios.create({
    baseURL,
    timeout: 30000,
    headers: { 'Content-Type': 'application/json' },
    withCredentials: true,
  });

  instance.interceptors.request.use(async (config) => {
    const store = (await import('../../redux/store.redux')).store;
    const { accessToken, userUnitPositionId, deviceId } = store.getState().account;
    if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`;
    if (userUnitPositionId) config.headers['userUnitPositionId'] = userUnitPositionId;
    if (deviceId) config.headers['deviceId'] = deviceId;
    return config;
  });

  instance.interceptors.response.use(
    (response) => (response.config.responseType === 'blob' ? response : response.data),
    async (error) => {
      const store = (await import('../../redux/store.redux')).store;
      const originalConfig = error.config;

      // Xử lý hết hạn token (HTTP status 777)
      if (error.status === HttpStatusSpecial.REFRESH_TOKEN) {
        const { ACTION_ACCOUNT } = await import('../../redux');
        const refreshToken = store.getState().account.refreshToken;
        const data = await store.dispatch(ACTION_ACCOUNT.refreshToken({ refreshToken })).unwrap();
        if (data.accessToken) originalConfig.headers.Authorization = `Bearer ${data.accessToken}`;
        return axios.request(originalConfig);
      }
      return Promise.reject(error.response?.data);
    }
  );

  return instance;
};

export const axiosRequest = createAxiosRequest(import.meta.env.VITE_BE_URL!);
export const axiosRequestSSO = createAxiosRequest(import.meta.env.VITE_SSO_BE_URL!);
```

---

## 4.3 Utilities bóc tách API & Xử lý lỗi

```typescript
// Path: src/common/utils/api-base-response.util.ts
export function unwrapBaseResponse<T>(result: unknown): T {
  if (typeof result !== 'object' || result === null) return result as T;
  const res = result as Record<string, unknown>;
  return (res.data ?? res.Data ?? result) as T;
}

export function getApiErrorMessage(error: unknown, fallback = 'Đã xảy ra lỗi không xác định!'): string {
  if (typeof error !== 'object' || error === null) return fallback;
  const body = error as { errors?: Record<string, string[]>; message?: string };
  
  // Ưu tiên hiển thị message validation tiếng Việt chi tiết từ FluentValidation
  if (body.errors) {
    const list = Object.values(body.errors).flat().filter(Boolean);
    if (list.length > 0) return list.join('; ');
  }
  return body.message?.trim() || fallback;
}
```

---

## 4.4 Dynamic Column Distinct Fetcher (Lọc cột)

```typescript
// Path: src/common/utils/column-distinct-fetcher.util.ts
export const createColumnDistinctFetcherForPrefix = (
  prefix: string,
  buildBody: (col: string, params: any) => any,
  post: (url: string, body: any) => Promise<any>
) => async (columnId: string, screenParams: any) => {
  const body = buildBody(columnId, screenParams);
  const res = await post(`${prefix}/column-distinct-values`, body);
  return unwrapBaseResponse(res);
};
```

---

## 4.5 Base Hooks: `useCustomSearchParams` & `useServerColumnFilters`

* **`useCustomSearchParams`:** Tự động phản chiếu bộ lọc, trang hiện tại, từ khóa tìm kiếm lên URL query (`?page=1&keyword=xyz`). Người dùng F5 hoặc copy link chia sẻ sẽ giữ nguyên ngữ cảnh.
* **`useServerColumnFilters`:** Quản lý danh sách giá trị lọc được chọn từ Dropdown trên từng cột, convert thành mảng `ColumnFilterDto[]` đẩy xuống BE.

---

## 4.6 Base Table Component: `TableLayered`
Nằm tại `src/components/table/table-layerd.component.tsx`:
* Hỗ trợ cấu hình cột dạng mảng `Column<T>[]`.
* Hỗ trợ bảng mở rộng lồng nhau (Collapsible Master-Detail).
* Hỗ trợ cấu trúc dạng Cây (Hierarchical Tree Data).
* Hỗ trợ chọn nhiều dòng qua Checkbox, Sort cột, Menu Action dòng (Popover).

---

# 5. END-TO-END FEATURE BLUEPRINT (FULL FLOW MẪU TỪ BE SANG FE)

Dưới đây là mã nguồn mẫu hoàn chỉnh khi tạo một tính năng mới: **Quản lý Lô Nguyên Liệu (`MaterialLot`)**.

### BƯỚC 1: BE – Tạo Entity trong Domain
```csharp
// src/SEVAGO.MaterialControl.Domain/Entities/MaterialLot/MaterialLot.cs
using SEVAGO.MaterialControl.Core.Domain.Common;
using SEVAGO.MaterialControl.Core.Domain.Shared.Attributes;
using SEVAGO.MaterialControl.Domain.Shared.Commons.Enums;

namespace SEVAGO.MaterialControl.Domain.Entities
{
    [SevagoTable("material_lot", Schema = "material_control_develop")]
    public class MaterialLot : BaseEntity, IHasAuditLog
    {
        [Filterable(ColumnVariant.Text)]
        public string LotNumber { get; set; }

        [Filterable(ColumnVariant.Text)]
        public string MaterialName { get; set; }

        public decimal Weight { get; set; }

        [Filterable(ColumnVariant.MultiSelect)]
        public MaterialLotStatus Status { get; set; }

        [Filterable(ColumnVariant.DateRange, UnixUnit = UnixTimestampUnit.Seconds)]
        public long ImportDate { get; set; }
    }
}
```

### BƯỚC 2: BE – Tạo Interface Repository
```csharp
// src/SEVAGO.MaterialControl.Domain/Entities/MaterialLot/Interfaces/IMaterialLotRepository.cs
using SEVAGO.MaterialControl.Core.DataAccess.Repository;

namespace SEVAGO.MaterialControl.Domain.Entities
{
    public interface IMaterialLotRepository : IMesRepository<MaterialLot, Guid>
    {
    }
}
```

### BƯỚC 3: BE – Cấu hình EF Core & Repo Implementation
```csharp
// src/SEVAGO.MaterialControl.EntityFrameworkCore/BuilderConfiguration/MaterialLotEntityTypeConfiguration.cs
public class MaterialLotEntityTypeConfiguration : EntityBaseConfiguration<MaterialLot>
{
    public override void Configure(EntityTypeBuilder<MaterialLot> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.LotNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Weight).HasPrecision(18, 4);
    }
}

// src/SEVAGO.MaterialControl.EntityFrameworkCore/Repositories/MaterialLotRepository.cs
public class MaterialLotRepository(
    IDbContextProvider<SevagoMaterialControlDbContext> dbContextProvider, 
    IObjectMapper objectMapper
) : MesRepository<SevagoMaterialControlDbContext, MaterialLot, Guid>(dbContextProvider, objectMapper), IMaterialLotRepository
{
}
```

### BƯỚC 4: BE – Tạo CQRS Contracts
```csharp
// Application.Contracts/Features/MaterialLotContracts/Queries/MaterialLotFilterQuery.cs
public class MaterialLotFilterQuery : BaseFilterQuery
{
    public MaterialLotStatus? Status { get; set; }
}

// Application.Contracts/Features/MaterialLotContracts/Results/MaterialLotResultDto.cs
public class MaterialLotResultDto
{
    public Guid Id { get; set; }
    public string LotNumber { get; set; }
    public string MaterialName { get; set; }
    public decimal Weight { get; set; }
    public MaterialLotStatus Status { get; set; }
    public long ImportDate { get; set; }
    public long CreatedOn { get; set; }
}

// Application.Contracts/Features/MaterialLotContracts/Services/IMaterialLotAppService.cs
public interface IMaterialLotAppService : IApplicationService
{
    Task<PaginatedResult<MaterialLotResultDto>> GetListAsync(MaterialLotFilterQuery query);
}
```

### BƯỚC 5: BE – Implement Application Service
```csharp
// src/SEVAGO.MaterialControl.Application/Features/MaterialLotApplication/Services/MaterialLotAppService.cs
public class MaterialLotAppService(
    IMaterialLotRepository lotRepo
) : SevagoMaterialControlAppService, IMaterialLotAppService
{
    public async Task<PaginatedResult<MaterialLotResultDto>> GetListAsync(MaterialLotFilterQuery query)
    {
        var (data, total) = await lotRepo.FilterProjectedDataAsync<MaterialLotResultDto>(
            filterFunc: q => q.Where(x => !x.IsDeleted),
            parameters: query,
            defaultOrder: q => q.OrderByDescending(x => x.CreatedOn)
        );

        return Success(data, total, query.Page, query.Take);
    }
}
```

### BƯỚC 6: BE – Tạo Minimal API Endpoint
```csharp
// src/SEVAGO.MaterialControl.HttpApi/Endpoints/MaterialLot/Handler.cs
public class MaterialLotHandler : IEndpointBase
{
    private const string _endpoint = "/material-lot";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost($"{_endpoint}/filter", async (MaterialLotFilterQuery request, IMaterialLotAppService svc) =>
            (await svc.GetListAsync(request)).CustomResult())
            .WithTags("Quản lý Lô Nguyên Liệu")
            .WithName("Lọc danh sách Lô")
            .MapToApiVersion(new(1, 0))
            .WithOpenApi()
            .RequirePerUserRateLimit()
            .RequireAuthorization();
    }
}
```

### BƯỚC 7: FE – Tạo API Client
```typescript
// src/apis/material-lot/material-lot.api.ts
import { axiosRequest } from '../../common/configs';
import { ResListResponse, PageOptionsDto } from '../../common/interfaces/api.interface';
import { MaterialLotItem } from './material-lot.interface';
import { createColumnDistinctFetcherForPrefix } from '../../common/utils/column-distinct-fetcher.util';
import { buildColumnDistinctRequestBody } from '../../common/utils/column-filter-api-body.util';

export const filterMaterialLots = async (params: PageOptionsDto): Promise<ResListResponse<MaterialLotItem>> => {
  return await axiosRequest.post('material-lot/filter', params);
};

export const fetchMaterialLotColumnDistinct = createColumnDistinctFetcherForPrefix(
  'material-lot',
  buildColumnDistinctRequestBody,
  axiosRequest.post
);
```

### BƯỚC 8: FE – Tạo Màn hình Screen & Table
```tsx
// src/screens/material-lot/material-lot.screen.tsx
import React, { useEffect, useState } from 'react';
import { Box, Typography } from '@mui/material';
import { useCustomSearchParams } from '../../hooks/use-custom-search-params.hook';
import { TableLayered } from '../../components/table/table-layerd.component';
import { filterMaterialLots } from '../../apis/material-lot/material-lot.api';
import { MaterialLotItem } from '../../apis/material-lot/material-lot.interface';
import { MATERIAL_LOT_COLUMNS } from './material-lot.constant';

export const MaterialLotScreen: React.FC = () => {
  const { mergedParams, setParams } = useCustomSearchParams({ page: 1, take: 20 });
  const [data, setData] = useState<{ list: MaterialLotItem[]; total: number }>({ list: [], total: 0 });
  const [loading, setLoading] = useState(false);

  const fetchData = async () => {
    try {
      setLoading(true);
      const res = await filterMaterialLots(mergedParams);
      setData({ list: res.list, total: res.total });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, [mergedParams]);

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h5" sx={{ mb: 2, fontWeight: 'bold' }}>Quản Lý Lô Nguyên Liệu</Typography>
      <TableLayered
        rows={data.list}
        columns={MATERIAL_LOT_COLUMNS}
        loading={loading}
        total={data.total}
        page={mergedParams.page}
        take={mergedParams.take}
        onChangePage={(page) => setParams({ page })}
        onChangeTake={(take) => setParams({ take, page: 1 })}
      />
    </Box>
  );
};
```

---

# 6. QUY TẮC VÀNG & KINH NGHIỆM THỰC CHIẾN (BEST PRACTICES & GOTCHAS)

1. **Chuẩn hóa Thời gian:**
   * Không bao giờ dùng `DateTime` trong Entity Database. Luôn dùng `long` Unix Timestamp tính theo **giây** (`DateTimeOffset.UtcNow.ToUnixTimeSeconds()`).
2. **Cơ chế Refresh Token (HTTP 777):**
   * Trong `axios.config.ts`, biến cờ refresh (`refreshingToken`) bắt buộc phải reset về `null` trong khối `finally { ... }`. Nếu không, chỉ cần một lần rớt mạng sẽ làm ứng dụng treo vĩnh viễn ở trạng thái refresh token lỗi.
3. **Soft Delete & Query Filter:**
   * Mọi câu query Entity phải luôn kèm điều kiện soft-delete: `.Where(x => !x.IsDeleted)`.
4. **Tránh Memory Leak trong Dependency Injection:**
   * Trong Minimal API Handler (`IEndpointBase`), inject các `AppService` trực tiếp vào lambda của endpoint, không capture trong constructor của class Handler.
5. **Đồng bộ hóa URL Params:**
   * Tất cả bộ lọc phân trang, tìm kiếm ở FE nên đi qua `useCustomSearchParams` để trải nghiệm người dùng không bị mất khi tải lại trang.
6. **Bóc tách lỗi Validation tiếng Việt:**
   * Luôn dùng hàm `getApiErrorMessage(error)` ở FE để ưu tiên đọc lỗi trong `errors.*` (FluentValidation) trước khi lấy `message` chung chung tiếng Anh.
