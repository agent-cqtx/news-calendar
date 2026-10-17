-- Manual: removes only the sample rows (cascades to sources/updates).
DELETE FROM events WHERE id::text LIKE 'e0000000-%';
