# ADR-005: Catalog Data versus Inventory

## Status
Accepted

## Context
A product page shows a name, a price, and whether the item can be bought. Those three facts change for different reasons and at different rates.

- Merchandising edits the name, description, and price a few times a day.
- The warehouse decrements stock on every order and receives shipments all day.
- A flash sale can make the stock row the hottest row in the platform while the catalog row is almost idle.

Putting `QuantityOnHand` on the product row forces the catalog database to absorb checkout writes. It also lets a product query lock the same row a reservation needs. That is the shared-database failure mode from ADR-001, inside a single table.

## Decision
The Product service owns the catalog and nothing else.

- `ProductDb` stores categories and products: SKU, name, description, price, currency, active flag.
- Quantity, reservations, and warehouses belong to the future Inventory service and `InventoryDb`.
- Product does not call Inventory to render a product page. The page can show catalog data without a second service. "In stock" is a separate read, composed later at the gateway or the client.
- SKU and currency are immutable after create. Inventory and Order will store the SKU. Changing it would orphan stock. Changing currency would silently reinterpret money.
- When an order is placed, Order will copy the SKU, name, and unit price onto the order line. It will not join `ProductDb`. The copy is the price the customer accepted. A later catalog edit does not rewrite history.

Redis caching of the catalog is deferred. The read model is already a projection with a page cap. A cache without an invalidation path would serve a stale price after an admin edit. Cache-aside belongs in the phase that introduces Redis, after the write path exists.

## Consequences

### Positive
- Catalog deploys and schema changes do not touch stock.
- Checkout contention stays in Inventory.
- Order history stays correct when a price changes tomorrow.

### Negative
- A product page that must show stock makes a second call, or the gateway aggregates two calls. That is extra latency and a failure mode: catalog up, inventory down. The UI must tolerate an unknown stock figure.
- Price on the catalog and price on an open cart can diverge until checkout snapshots the current price. The cart phase has to decide whether to refresh the price at checkout.
