# InventoryManager — SAP → ERPNext Migration Guide

> **Date**: September 2026  
> **Status**: Planning  
> **Estimated Effort**: 2–3 days (development + testing)

---

## Table of Contents

1. [Overview](#1-overview)
2. [Pre-Migration Checklist (ERPNext Setup)](#2-pre-migration-checklist-erpnext-setup)
3. [Step 1 — ERPNext API Access](#step-1--erpnext-api-access)
4. [Step 2 — Create Custom Fields in ERPNext](#step-2--create-custom-fields-in-erpnext)
5. [Step 3 — Warehouse Mapping](#step-3--warehouse-mapping)
6. [Step 4 — Code Changes](#step-4--code-changes)
7. [Step 5 — Configuration Update](#step-5--configuration-update)
8. [Step 6 — Testing](#step-6--testing)
9. [Step 7 — Go-Live Cutover](#step-7--go-live-cutover)
10. [WooCommerce & Dependent Systems](#woocommerce--dependent-systems)
11. [Rollback Plan](#rollback-plan)
12. [Reference: SAP → ERPNext Field Mapping](#reference-sap--erpnext-field-mapping)

---

## 1. Overview

The InventoryManager is a one-way stock sync tool:

```
ERP (source of truth) ──► InventoryManager ──► WooCommerce (petstore.co.ke)
```

Currently the ERP source is **SAP Business One** accessed via direct SQL queries. We are migrating to **ERPNext** accessed via its REST API. The WooCommerce side is **completely unaffected**.

### What Changes
- Data source: SAP SQL Server → ERPNext REST API
- Connection method: `SqlConnection` → `HttpClient` (JSON)
- Product model: `SapProduct` → `ErpNextProduct`

### What Stays the Same
- WooCommerce read/write (all REST API calls)
- SKU matching logic (`@SE`, `@D`, `$X` conventions)
- Bulk pack stock calculation
- Email reporting
- Short expiry & damaged goods workflows

---

## 2. Pre-Migration Checklist (ERPNext Setup)

Complete these **before** writing any code:

- [ ] ERPNext instance is live and accessible (note the URL)
- [ ] All items/products have been migrated into ERPNext's **Item** doctype
- [ ] All warehouses have been created in ERPNext
- [ ] Stock Opening Entries have been posted (so `Bin` records exist with `actual_qty`)
- [ ] An API user has been created with the correct permissions
- [ ] Custom fields have been created (see Step 2 below)

---

## Step 1 — ERPNext API Access

### 1.1 Create an API User

In ERPNext:
1. Go to **Settings → User** and create a dedicated user (e.g., `sync@petstore.co.ke`)
2. Assign the role **Stock User** (read-only is sufficient)
3. Generate API keys:
   - Go to the user's page → **API Access** section
   - Click **Generate Keys**
   - Save the **API Key** and **API Secret** securely

### 1.2 Test API Connectivity

```bash
# Test basic connectivity
curl -s https://YOUR-ERPNEXT-URL/api/resource/Item?limit_page_length=1 \
  -H "Authorization: token API_KEY:API_SECRET"

# Test stock data
curl -s "https://YOUR-ERPNEXT-URL/api/resource/Bin?filters=[[\"warehouse\",\"=\",\"Main Warehouse - PSK\"]]&fields=[\"item_code\",\"actual_qty\"]&limit_page_length=5" \
  -H "Authorization: token API_KEY:API_SECRET"
```

✅ If both return JSON data, you're good to proceed.

---

## Step 2 — Create Custom Fields in ERPNext

The SAP database had User Defined Fields (UDFs) that must be replicated in ERPNext.

Go to **Customization → Custom Field** in ERPNext and create:

| Field Name | Label | Doctype | Type | Default | Purpose |
|-----------|-------|---------|------|---------|---------|
| `custom_web_item` | Web Item | Item | Check | 0 | Replaces SAP's `U_WebItem` — marks items for WooCommerce sync |
| `custom_is_bulk_pack` | Is Bulk Pack | Item | Check | 0 | Replaces SAP's `U_BlPack` — identifies bulk pack items |
| `custom_bulk_pack_qty` | Bulk Pack Quantity | Item | Int | 0 | Replaces SAP's `U_BlQty` — the number of units in a bulk pack |
| `custom_bulk_pack_web_id` | Bulk Pack Web ID | Item | Data | | Replaces SAP's `U_BlWebId` |

### Verification

```bash
# Confirm custom fields are queryable
curl -s "https://YOUR-ERPNEXT-URL/api/resource/Item?fields=[\"item_code\",\"custom_web_item\",\"custom_is_bulk_pack\",\"custom_bulk_pack_qty\"]&limit_page_length=3" \
  -H "Authorization: token API_KEY:API_SECRET"
```

---

## Step 3 — Warehouse Mapping

SAP uses numeric warehouse codes. ERPNext uses descriptive names.

Create this mapping and keep it in the config:

| SAP Warehouse Code | SAP Purpose | ERPNext Warehouse Name (example) |
|-------------------|-------------|----------------------------------|
| `01` | Main warehouse | `Main Warehouse - PSK` |
| `06` | Secondary warehouse | `Secondary Warehouse - PSK` |
| `02` | Damaged goods | `Damaged Goods - PSK` |
| `04` | Short expiry | `Short Expiry - PSK` |

> ⚠️ **Action Required**: Confirm the exact ERPNext warehouse names with the ERPNext admin and update the table above before proceeding.

---

## Step 4 — Code Changes

### 4.1 New Files to Create

#### `Models/ErpNextProduct.cs`
```csharp
public class ErpNextProduct
{
    public string ItemCode { get; set; }       // Maps to: item_code
    public string ItemName { get; set; }       // Maps to: item_name
    public decimal StockQty { get; set; }      // Computed: sum of Bin.actual_qty
    public string IsBlPackItem { get; set; }   // Maps to: custom_is_bulk_pack (Y/N)
    public int BlPackQuantity { get; set; }    // Maps to: custom_bulk_pack_qty
}
```

#### `Services/ErpNextService.cs`
New service class that:
- Uses `HttpClient` with `Authorization: token KEY:SECRET` header
- Fetches items from `GET /api/resource/Item` with filters
- Fetches stock from `GET /api/resource/Bin` grouped by item + warehouse
- Returns `List<ErpNextProduct>` with aggregated stock quantities

Key API calls:
```
GET /api/resource/Item
  ?filters=[["disabled","=",0],["custom_web_item","=",1]]
  &fields=["item_code","item_name","custom_is_bulk_pack","custom_bulk_pack_qty","custom_bulk_pack_web_id"]
  &limit_page_length=0

GET /api/resource/Bin
  ?filters=[["warehouse","in",["Main Warehouse - PSK","Secondary Warehouse - PSK"]]]
  &fields=["item_code","actual_qty","warehouse"]
  &limit_page_length=0
```

### 4.2 Files to Modify

#### `Settings.cs`
- **Remove**: `ConnectionString` property  
- **Add**: `ErpNextUrl`, `ErpNextApiKey`, `ErpNextApiSecret`

#### `Models/Config.cs`
- **Add**: ERPNext connection config properties
- **Change**: `WarehousesToIncludeInInventory` from `List<string>` of codes to ERPNext warehouse names

#### `Services/ProductsService.cs`
- **Remove**: `QuerySapProducts()` method
- **Remove**: `GetAllProductsSqlString()` method
- **Remove**: `using System.Data.SqlClient;`
- **Replace**: All `SapProduct` references with `ErpNextProduct`
- **Inject**: `ErpNextService` and call its methods instead

### 4.3 Files to Delete

| File | Reason |
|------|--------|
| `Models/SapProduct.cs` | Replaced by `ErpNextProduct.cs` |
| `Common/ReflectPropertyInfo.cs` | Only used for SQL DataReader mapping |
| `Common/Attributes/DataFieldAttribute.cs` | Only used by `ReflectPropertyInfo` |

### 4.4 Project File Update

#### `InventoryManager.csproj`
- Remove `System.Data.SqlClient` reference
- Add new file references for `ErpNextProduct.cs` and `ErpNextService.cs`
- Remove references to deleted files

---

## Step 5 — Configuration Update

Update `appsettings.json`:

```json
{
  "ErpNextUrl": "https://YOUR-ERPNEXT-URL",
  "ErpNextApiKey": "your-api-key-here",
  "ErpNextApiSecret": "your-api-secret-here",
  "WarehousesToIncludeInInventory": [
    "Main Warehouse - PSK",
    "Secondary Warehouse - PSK"
  ],
  "ShortExpiryWarehouse": "Short Expiry - PSK",
  "DamagedGoodsWarehouse": "Damaged Goods - PSK",
  "SyncronizerReportReceivers": ["martin@granularit.com"],
  "ProductsToSkip": null,
  "ProductsToUpdate": null
}
```

> 🔐 **Security Note**: Do NOT commit API keys to Git. Use environment variables or a secrets manager in production.

---

## Step 6 — Testing

### 6.1 Unit / Smoke Testing

1. Set `"ProductsToUpdate": 5` in `appsettings.json` to test a small batch
2. Run in `DEBUG` mode pointing to the staging WooCommerce site (`petstore.four.africa`)
3. Check console output for:
   - Correct number of items fetched from ERPNext
   - Stock quantities making sense
   - No connection/auth errors

### 6.2 Validation Checklist

- [ ] ERPNext API returns items with `custom_web_item = 1`
- [ ] Stock quantities match what ERPNext shows in the UI
- [ ] Bulk pack items are identified (`custom_is_bulk_pack`)
- [ ] Bulk pack stock calculation is correct (integer division)
- [ ] Short expiry items (warehouse `Short Expiry`) are fetched correctly
- [ ] Damaged goods items (warehouse `Damaged Goods`) are fetched correctly
- [ ] WooCommerce products get updated with correct stock levels
- [ ] Products with zero stock in short expiry/damaged → set to `draft` on WC
- [ ] Email report sends and lists missing products correctly
- [ ] SKU matching still works for all product types:
  - Normal products: `SKU` matches `item_code`
  - Short expiry: `SKU@SE` matches
  - Damaged: `SKU@DE` matches
  - Bulk packs: `SKU$24` matches

### 6.3 Parallel Run (Recommended)

If possible, run both the old SAP sync and the new ERPNext sync against **staging** simultaneously for 1 week and compare outputs. This catches edge cases.

---

## Step 7 — Go-Live Cutover

### Pre-Cutover
- [ ] All tests pass on staging
- [ ] ERPNext stock data is verified as accurate
- [ ] Production `appsettings.json` is prepared with real credentials
- [ ] Backup the current production InventoryManager executable

### Cutover Day
1. **Stop** the current scheduled task / cron job running the SAP sync
2. **Deploy** the new ERPNext-connected InventoryManager
3. **Run manually** once and verify WooCommerce stock levels
4. **Re-enable** the scheduled task
5. **Monitor** email reports for the first 2-3 days

### Post-Cutover
- [ ] Verify email reports show sensible data
- [ ] Spot-check 20 products on WooCommerce vs ERPNext
- [ ] Confirm dependent systems are unaffected
- [ ] Remove old SAP connection strings from any config files

---

## WooCommerce & Dependent Systems

### Impact Assessment: **NONE**

The InventoryManager writes to WooCommerce using the **exact same REST API calls** regardless of whether data comes from SAP or ERPNext. The data format pushed to WC is:

```json
{
  "update": [
    { "id": 123, "stock_quantity": 50, "status": "publish" },
    { "id": 456, "stock_quantity": 0, "status": "draft" }
  ]
}
```

This format does **not change**. Any system reading from WooCommerce (other integrations, mobile apps, etc.) will see no difference.

### What Dependent Systems Should Know
- **No API changes** on the WooCommerce side
- **No SKU format changes** — all conventions (`@SE`, `@D`, `$X`) remain identical
- **Sync frequency** can stay the same (or be adjusted independently)
- The only change: inventory source of truth is now ERPNext instead of SAP

---

## Rollback Plan

If issues arise after go-live:

1. **Stop** the new InventoryManager
2. **Restore** the backup of the old SAP-connected executable
3. **Revert** `appsettings.json` to the SAP configuration
4. **Restart** the scheduled task

The old SAP database is still available during the transition period, so rollback is safe.

---

## Reference: SAP → ERPNext Field Mapping

| SAP Table | SAP Field | ERPNext Doctype | ERPNext Field | Notes |
|----------|-----------|----------------|---------------|-------|
| `OITM` | `ItemCode` | Item | `item_code` | Primary identifier |
| `OITM` | `ItemName` | Item | `item_name` | Display name |
| `OITM` | `validFor` | Item | `disabled` | `validFor='Y'` → `disabled=0` |
| `OITM` | `U_WebItem` | Item | `custom_web_item` | Custom field (must create) |
| `OITM` | `U_BlPack` | Item | `custom_is_bulk_pack` | Custom field (must create) |
| `OITM` | `U_BlQty` | Item | `custom_bulk_pack_qty` | Custom field (must create) |
| `OITM` | `U_BlWebId` | Item | `custom_bulk_pack_web_id` | Custom field (must create) |
| `OITW` | `OnHand` | Bin | `actual_qty` | Per-warehouse stock level |
| `OITW` | `WhsCode` | Bin | `warehouse` | Full name instead of code |
| `OWHS` | `WhsCode` | Warehouse | `name` | Warehouse identifier |

---

## Contacts

- **Developer**: developers@granularit.com
- **ERPNext Admin**: _(fill in)_
- **WooCommerce Admin**: _(fill in)_

---

*This guide should be kept updated as decisions are made. Check off items as they are completed.*
