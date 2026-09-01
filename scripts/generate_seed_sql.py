#!/usr/bin/env python3
"""
Generate SQL Server INSERT statements for the Stationery Management System's business
tables: Categories, Suppliers, StationeryItems, StockTransactions, Requests, RequestItems,
RequestStatusHistory, SupplierRequests, SupplierRequestItems.

Deliberately does NOT generate AspNetUsers / AspNetRoles rows. ASP.NET Core Identity needs
a specific PasswordHash / SecurityStamp / ConcurrencyStamp format (PBKDF2-HMAC-SHA256 with a
particular header byte layout) that isn't safe or meaningful to fake from outside the app —
inserting garbage there would produce accounts that can never log in and could violate the
NormalizedUserName/NormalizedEmail unique indexes. Create real users first (via
DbSeeder.SeedBootstrapAdminAsync + POST /api/v1/users, or the User Management UI), then pass
--employee-ids with the EXACT employee numbers that exist — e.g. run
`SELECT Id FROM AspNetUsers ORDER BY Id;` against your database and paste the results in.

An earlier version of this script took --employee-min/--employee-max and assumed every number
in between existed (a dense range). That is wrong whenever real employee numbers are sparse
(e.g. just #1, or #1/#3/#7) — SQL Server rejects the whole INSERT batch the first time it hits
a number that isn't a real row, some tables further down the dependency chain end up empty, and
their own FK-referencing tables then fail too (this exact failure mode happened on
SupplierRequests -> SupplierRequestItems on 2026-08-30). --employee-ids removes that foot-gun
by only ever picking numbers you've confirmed are real.

Two columns ARE enforced by a real FK to AspNetUsers and will fail on insert if the
employee number doesn't exist: StockTransactions.CreatedByEmployeeNumber and
SupplierRequests.CreatedByEmployeeNumber. Requests.RequestorEmployeeNumber/
ApproverEmployeeNumber and RequestStatusHistory.ActorEmployeeNumber have no DB-level FK
(confirmed against Infrastructure/Data/Migrations/20260830072208_AddRequestEntities.cs), but
should still reference real employee numbers for the data to make sense.

Requires only the Python 3 standard library — nothing to pip install.

Usage:
    python3 scripts/generate_seed_sql.py --employee-ids 1,2,5,9 --output seed.sql

    # Reproducible output, smaller dataset, IDs starting above what DbSeeder already seeds:
    python3 scripts/generate_seed_sql.py \\
        --seed 42 --start-id 1000 \\
        --categories 5 --suppliers 6 --items 40 \\
        --requests 30 --supplier-requests 10 \\
        --employee-ids 1,2,5,9 \\
        --output seed.sql

Run the output against an EMPTY set of these tables, or raise --start-id past whatever IDs
already exist — the script uses SET IDENTITY_INSERT to assign explicit, predictable IDs so
foreign keys between the generated rows line up, and that will collide with existing rows at
the same IDs (e.g. DbSeeder's own 5 categories / 6 suppliers / 40 items, all at IDs 1..N).
"""

from __future__ import annotations

import argparse
import random
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timedelta

# --- Reference data -----------------------------------------------------------------

CATEGORY_NAMES = [
    "Paper & Notebooks",
    "Writing Instruments",
    "Tech & Accessories",
    "Organization",
    "Printing Supplies",
]
SUPPLIER_NAMES = [
    "OfficeMax Direct",
    "Global Paper Co.",
    "Metro Stationery Wholesale",
    "TechSupply Partners",
    "EcoWrite Manufacturing",
    "Budget Bulk Supplies",
]
UNIT_OF_MEASURE = ["Each", "Box", "Pack", "Ream", "Case"]
ITEM_NOUNS = [
    "Notebook", "Pen Set", "Stapler", "Binder", "Marker Pack", "Sticky Notes",
    "Desk Organizer", "Whiteboard", "USB Drive", "Mouse Pad", "Highlighter Set",
    "Envelope Box", "Clipboard", "Label Maker Tape", "Correction Tape",
]
ITEM_ADJECTIVES = ["Standard", "Premium", "Compact", "Heavy-Duty", "Recycled", "Deluxe"]

# StockTransactionType enum order in Core/Entities/StockTransactionType.cs — stored as int,
# not string (no HasConversion<string>() anywhere; confirmed against the migration's
# `TxType = table.Column<int>(...)`).
TX_TYPE_RECEIPT, TX_TYPE_ISSUE, TX_TYPE_ADJUSTMENT = 0, 1, 2

# CK_Requests_Status allows exactly these 8 values.
REQUEST_STATUSES = [
    "Pending", "Approved", "PartiallyApproved", "Rejected",
    "Withdrawn", "CancellationPending", "Cancelled", "Fulfilled",
]
TERMINAL_STATUSES = {"Approved", "PartiallyApproved", "Rejected", "Withdrawn", "Cancelled", "Fulfilled"}

EPOCH_START = datetime(2026, 1, 1)
EPOCH_END = datetime(2026, 8, 28)


# --- SQL formatting helpers ----------------------------------------------------------

def sql_str(value: str | None) -> str:
    if value is None:
        return "NULL"
    return "'" + value.replace("'", "''") + "'"


def sql_int(value: int | None) -> str:
    return "NULL" if value is None else str(value)


def sql_bit(value: bool) -> str:
    return "1" if value else "0"


def sql_decimal(value: float) -> str:
    return f"{value:.2f}"


def sql_datetime(dt: datetime | None) -> str:
    return "NULL" if dt is None else f"'{dt.strftime('%Y-%m-%dT%H:%M:%S')}'"


def sql_guid(value: uuid.UUID | None = None) -> str:
    return f"'{value or uuid.uuid4()}'"


def random_datetime(rng: random.Random, start: datetime = EPOCH_START, end: datetime = EPOCH_END) -> datetime:
    span = int((end - start).total_seconds())
    return start + timedelta(seconds=rng.randint(0, span))


# --- Table writer ----------------------------------------------------------------------

class TableWriter:
    """Buffers one multi-row INSERT per table, wrapped in SET IDENTITY_INSERT so explicit
    IDs (and therefore FK references between generated rows) are predictable."""

    def __init__(self) -> None:
        self.blocks: list[str] = []

    def insert_all(self, table: str, columns: list[str], rows: list[list[str]]) -> None:
        if not rows:
            return
        col_list = ", ".join(f"[{c}]" for c in columns)
        values = ",\n    ".join("(" + ", ".join(row) + ")" for row in rows)
        self.blocks.append(
            f"SET IDENTITY_INSERT [{table}] ON;\n"
            f"INSERT INTO [{table}] ({col_list})\nVALUES\n    {values};\n"
            f"SET IDENTITY_INSERT [{table}] OFF;\n"
        )

    def dump(self) -> str:
        return "\n".join(self.blocks)


@dataclass
class GeneratedIds:
    category_ids: list[int] = field(default_factory=list)
    supplier_ids: list[int] = field(default_factory=list)
    item_ids: list[int] = field(default_factory=list)
    request_ids: list[int] = field(default_factory=list)


# --- Generators, one per table, in FK dependency order --------------------------------

def generate_categories(writer: TableWriter, ids: GeneratedIds, next_id, rng: random.Random, count: int) -> None:
    rows = []
    for name in CATEGORY_NAMES[:count] or [f"Category {i}" for i in range(count)]:
        cid = next_id()
        ids.category_ids.append(cid)
        rows.append([sql_int(cid), sql_str(name), sql_bit(rng.random() > 0.05)])
    writer.insert_all("Categories", ["Id", "Name", "IsActive"], rows)


def generate_suppliers(writer: TableWriter, ids: GeneratedIds, next_id, rng: random.Random, count: int) -> None:
    rows = []
    for i in range(count):
        sid = next_id()
        ids.supplier_ids.append(sid)
        name = SUPPLIER_NAMES[i % len(SUPPLIER_NAMES)]
        rows.append([
            sql_int(sid),
            sql_str(name),
            sql_int(rng.randint(1, 30)),
            sql_bit(rng.random() > 0.1),
            sql_guid(),
        ])
    writer.insert_all("Suppliers", ["Id", "Name", "LeadTimeDays", "IsActive", "RowVersion"], rows)


def generate_stationery_items(writer: TableWriter, ids: GeneratedIds, next_id, rng: random.Random, count: int) -> None:
    rows = []
    for _ in range(count):
        iid = next_id()
        ids.item_ids.append(iid)
        name = f"{rng.choice(ITEM_ADJECTIVES)} {rng.choice(ITEM_NOUNS)}"
        category_id = rng.choice(ids.category_ids)
        supplier_id = rng.choice(ids.supplier_ids) if rng.random() > 0.2 else None
        reorder_level = rng.randint(5, 100)
        rows.append([
            sql_int(iid),
            sql_str(name),
            sql_int(category_id),
            sql_int(supplier_id),
            sql_str(rng.choice(UNIT_OF_MEASURE)),
            sql_decimal(round(rng.uniform(0.5, 100), 2)),
            sql_int(0),  # QuantityAvailable is fixed up after StockTransactions are generated.
            sql_int(reorder_level),
            sql_int(rng.randint(1, 4)),
            sql_bit(rng.random() > 0.1),
            sql_guid(),
        ])
    writer.insert_all(
        "StationeryItems",
        ["Id", "ItemName", "CategoryId", "SupplierId", "UnitOfMeasure", "UnitCost",
         "QuantityAvailable", "ReorderLevel", "MinRankLevelToRequest", "IsActive", "RowVersion"],
        rows,
    )
    return {row_id: reorder for row_id, reorder in zip(ids.item_ids, (int(r[7]) for r in rows))}


def generate_stock_transactions(
    writer: TableWriter, ids: GeneratedIds, next_id, rng: random.Random,
    reorder_levels: dict[int, int], employee_ids: list[int],
) -> dict[int, int]:
    """Returns the final QuantityAvailable per item so the caller can patch StationeryItems —
    this script emits StationeryItems before StockTransactions (FK direction requires it), so
    the balance fixup is a second UPDATE statement rather than an inline value."""
    rows = []
    final_balance: dict[int, int] = {}

    for item_id in ids.item_ids:
        reorder_level = reorder_levels[item_id]
        balance = reorder_level * 3
        opening_id = next_id()
        rows.append([
            sql_int(opening_id), sql_int(item_id), sql_int(TX_TYPE_RECEIPT), sql_int(balance),
            sql_decimal(round(rng.uniform(0.5, 100), 2)), sql_str("OPENING"),
            sql_int(rng.choice(ids.supplier_ids)),
            sql_datetime(EPOCH_START), sql_int(rng.choice(employee_ids)),
        ])

        num_events = rng.randint(5, 15)
        for _ in range(num_events):
            roll = rng.random()
            if roll < 0.7:
                change = -min(balance, rng.randint(1, 6))
                if change == 0:
                    continue
                tx_type, reference, supplier_id = TX_TYPE_ISSUE, None, None
            elif roll < 0.9:
                change = rng.randint(10, 30)
                tx_type, reference, supplier_id = TX_TYPE_RECEIPT, f"PO-{rng.randint(1000, 9999)}", rng.choice(ids.supplier_ids)
            else:
                change = rng.choice([2, -min(balance, 2)])
                if change == 0:
                    continue
                tx_type, reference, supplier_id = TX_TYPE_ADJUSTMENT, None, None

            balance += change
            tx_id = next_id()
            rows.append([
                sql_int(tx_id), sql_int(item_id), sql_int(tx_type), sql_int(change),
                sql_decimal(round(rng.uniform(0.5, 100), 2)), sql_str(reference),
                sql_int(supplier_id), sql_datetime(random_datetime(rng)),
                sql_int(rng.choice(employee_ids)),
            ])

        final_balance[item_id] = balance

    writer.insert_all(
        "StockTransactions",
        ["Id", "ItemId", "TxType", "ChangeQuantity", "UnitCostSnapshot", "Reference",
         "SupplierId", "CreatedAtUtc", "CreatedByEmployeeNumber"],
        rows,
    )
    return final_balance


def generate_quantity_available_fixup(writer: TableWriter, final_balance: dict[int, int]) -> None:
    """StationeryItems.QuantityAvailable must equal SUM(StockTransactions.ChangeQuantity) per
    item (the ledger invariant DbSeeder/StockService both maintain) — patch it after the fact
    since StationeryItems had to be inserted before StockTransactions could reference them."""
    lines = ["-- Reconcile cached balances with the ledger just written above."]
    for item_id, balance in final_balance.items():
        lines.append(f"UPDATE [StationeryItems] SET [QuantityAvailable] = {balance} WHERE [Id] = {item_id};")
    writer.blocks.append("\n".join(lines) + "\n")


def generate_requests(
    writer: TableWriter, ids: GeneratedIds, next_id, rng: random.Random, count: int,
    employee_ids: list[int],
) -> None:
    rows = []
    for _ in range(count):
        rid = next_id()
        ids.request_ids.append(rid)
        requestor = rng.choice(employee_ids)
        approver = rng.choice(employee_ids) if rng.random() > 0.05 else None
        status = rng.choice(REQUEST_STATUSES)
        decided = random_datetime(rng) if status in TERMINAL_STATUSES else None
        rows.append([
            sql_int(rid), sql_int(requestor), sql_int(approver), sql_str(status),
            sql_decimal(0),  # TotalEstimatedCost is patched after RequestItems are generated.
            sql_datetime(random_datetime(rng, EPOCH_END, EPOCH_END + timedelta(days=120)))
            if rng.random() > 0.3 else "NULL",
            sql_str("Approved as requested." if status in ("Approved", "PartiallyApproved") else None),
            sql_datetime(random_datetime(rng)),
            sql_datetime(decided),
            sql_guid(),
        ])
    writer.insert_all(
        "Requests",
        ["Id", "RequestorEmployeeNumber", "ApproverEmployeeNumber", "Status", "TotalEstimatedCost",
         "RequiredByDate", "DecisionComment", "CreatedAtUtc", "DecidedAtUtc", "RowVersion"],
        rows,
    )


def generate_request_items(writer: TableWriter, ids: GeneratedIds, next_id, rng: random.Random) -> dict[int, float]:
    rows = []
    totals: dict[int, float] = {rid: 0.0 for rid in ids.request_ids}
    for request_id in ids.request_ids:
        for item_id in rng.sample(ids.item_ids, k=min(len(ids.item_ids), rng.randint(1, 5))):
            ri_id = next_id()
            quantity = rng.randint(1, 20)
            unit_cost = round(rng.uniform(0.5, 100), 2)
            line_total = round(quantity * unit_cost, 2)
            totals[request_id] += line_total
            rows.append([
                sql_int(ri_id), sql_int(request_id), sql_int(item_id),
                sql_int(quantity), sql_decimal(unit_cost), sql_decimal(line_total),
            ])
    writer.insert_all(
        "RequestItems",
        ["Id", "RequestId", "ItemId", "Quantity", "UnitCostSnapshot", "LineTotal"],
        rows,
    )
    return totals


def generate_total_estimated_cost_fixup(writer: TableWriter, totals: dict[int, float]) -> None:
    lines = ["-- TotalEstimatedCost = SUM(RequestItems.LineTotal) for its request (CLAUDE.md principle #8: snapshot, not recomputed at read time — patched once here at seed time)."]
    for request_id, total in totals.items():
        lines.append(f"UPDATE [Requests] SET [TotalEstimatedCost] = {total:.2f} WHERE [Id] = {request_id};")
    writer.blocks.append("\n".join(lines) + "\n")


def generate_request_status_history(
    writer: TableWriter, ids: GeneratedIds, next_id, rng: random.Random,
    employee_ids: list[int],
) -> None:
    rows = []
    for request_id in ids.request_ids:
        rows.append([
            sql_int(next_id()), sql_int(request_id), "NULL", sql_str("Pending"),
            sql_int(rng.choice(employee_ids)), "NULL", sql_datetime(random_datetime(rng)),
        ])
        if rng.random() > 0.3:
            to_status = rng.choice(REQUEST_STATUSES[1:])
            rows.append([
                sql_int(next_id()), sql_int(request_id), sql_str("Pending"), sql_str(to_status),
                sql_int(rng.choice(employee_ids)),
                sql_str("Decision recorded." if rng.random() > 0.5 else None),
                sql_datetime(random_datetime(rng)),
            ])
    writer.insert_all(
        "RequestStatusHistory",
        ["Id", "RequestId", "FromStatus", "ToStatus", "ActorEmployeeNumber", "Comment", "CreatedAtUtc"],
        rows,
    )


def generate_supplier_requests(
    writer: TableWriter, ids: GeneratedIds, next_id, rng: random.Random, count: int,
    employee_ids: list[int],
) -> list[int]:
    rows = []
    sr_ids = []
    for _ in range(count):
        sr_id = next_id()
        sr_ids.append(sr_id)
        rows.append([
            sql_int(sr_id), sql_int(rng.choice(ids.supplier_ids)),
            sql_decimal(0),  # TotalCost is patched after SupplierRequestItems are generated.
            sql_datetime(random_datetime(rng)), sql_int(rng.choice(employee_ids)),
        ])
    writer.insert_all(
        "SupplierRequests",
        ["Id", "SupplierId", "TotalCost", "CreatedAtUtc", "CreatedByEmployeeNumber"],
        rows,
    )
    return sr_ids


def generate_supplier_request_items(
    writer: TableWriter, ids: GeneratedIds, next_id, rng: random.Random, sr_ids: list[int],
) -> dict[int, float]:
    rows = []
    totals: dict[int, float] = {sr_id: 0.0 for sr_id in sr_ids}
    for sr_id in sr_ids:
        # Unique (SupplierRequestId, ItemId) — sample without replacement per order.
        for item_id in rng.sample(ids.item_ids, k=min(len(ids.item_ids), rng.randint(1, 4))):
            sri_id = next_id()
            quantity = rng.randint(1, 100)
            unit_cost = round(rng.uniform(0.5, 100), 2)
            line_total = round(quantity * unit_cost, 2)
            totals[sr_id] += line_total
            rows.append([
                sql_int(sri_id), sql_int(sr_id), sql_int(item_id),
                sql_int(quantity), sql_decimal(unit_cost), sql_decimal(line_total),
            ])
    writer.insert_all(
        "SupplierRequestItems",
        ["Id", "SupplierRequestId", "ItemId", "Quantity", "UnitCostSnapshot", "LineTotal"],
        rows,
    )
    return totals


def generate_supplier_request_total_fixup(writer: TableWriter, totals: dict[int, float]) -> None:
    lines = ["-- TotalCost = SUM(SupplierRequestItems.LineTotal) for its order."]
    for sr_id, total in totals.items():
        lines.append(f"UPDATE [SupplierRequests] SET [TotalCost] = {total:.2f} WHERE [Id] = {sr_id};")
    writer.blocks.append("\n".join(lines) + "\n")


# --- Entry point -----------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--categories", type=int, default=5)
    parser.add_argument("--suppliers", type=int, default=6)
    parser.add_argument("--items", type=int, default=40)
    parser.add_argument("--requests", type=int, default=30)
    parser.add_argument("--supplier-requests", type=int, default=10)
    parser.add_argument(
        "--employee-ids", type=str, required=True,
        help="Comma-separated list of employee numbers that ACTUALLY exist in AspNetUsers "
             "right now — e.g. '1,2,5,9'. Run `SELECT Id FROM AspNetUsers ORDER BY Id;` "
             "against your database first. Do not guess a range; sparse gaps will cause a "
             "foreign-key violation the first time an unlucky pick lands in one.")
    parser.add_argument("--start-id", type=int, default=1,
                         help="First identity value to assign, per table. Raise this if the "
                              "target tables already have rows in this ID range (default: 1).")
    parser.add_argument("--seed", type=int, default=None, help="Random seed, for reproducible output.")
    parser.add_argument("--output", type=str, default="seed.sql")
    args = parser.parse_args()

    try:
        employee_ids = sorted({int(x.strip()) for x in args.employee_ids.split(",") if x.strip()})
    except ValueError:
        parser.error("--employee-ids must be a comma-separated list of integers, e.g. '1,2,5,9'")
    if not employee_ids:
        parser.error("--employee-ids must contain at least one employee number")

    rng = random.Random(args.seed)
    writer = TableWriter()
    ids = GeneratedIds()

    # One shared identity counter is simplest, at the cost of tables not each starting at 1 —
    # every table's SET IDENTITY_INSERT block only cares that IDs are unique within that table.
    counter = {"value": args.start_id}

    def next_id() -> int:
        value = counter["value"]
        counter["value"] += 1
        return value

    generate_categories(writer, ids, next_id, rng, args.categories)
    generate_suppliers(writer, ids, next_id, rng, args.suppliers)
    reorder_levels = generate_stationery_items(writer, ids, next_id, rng, args.items)
    final_balances = generate_stock_transactions(
        writer, ids, next_id, rng, reorder_levels, employee_ids)
    generate_quantity_available_fixup(writer, final_balances)

    generate_requests(writer, ids, next_id, rng, args.requests, employee_ids)
    request_totals = generate_request_items(writer, ids, next_id, rng)
    generate_total_estimated_cost_fixup(writer, request_totals)
    generate_request_status_history(writer, ids, next_id, rng, employee_ids)

    sr_ids = generate_supplier_requests(
        writer, ids, next_id, rng, args.supplier_requests, employee_ids)
    sr_totals = generate_supplier_request_items(writer, ids, next_id, rng, sr_ids)
    generate_supplier_request_total_fixup(writer, sr_totals)

    header = (
        "-- Generated by scripts/generate_seed_sql.py — synthetic test data, not real records.\n"
        "-- Does NOT include AspNetUsers/AspNetRoles; run against a database where these\n"
        f"-- employee numbers already exist: {', '.join(str(e) for e in employee_ids)}.\n\n"
    )

    with open(args.output, "w", encoding="utf-8") as f:
        f.write(header)
        f.write(writer.dump())

    print(f"Wrote {args.output}")


if __name__ == "__main__":
    main()
