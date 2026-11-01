-- Server panel: adds, removes or sets one item in the stock of a test player's city, or shows the whole stock (CR-0006).
-- A developer tool for the local dev database, run by `panel.sh goods ...` through psql. It is not part of the game: no endpoint and no
-- client code reach it (core-instructions/02-principles.md rule 5 is about those). panel.sh checks that the item is in the content file.
--
-- Run:  psql -X -q -v ON_ERROR_STOP=1 -v player=devtest -v mode=add -v item=planks -v amount=50 -f goods.sql
--   player  the dev name of the player; panel.sh passes the one the Unity client logs in as, or $PLAYER (CR-0006-T2)
--   mode    show | add | remove | set
--   item    an item id of the content (ignored by show)
--   amount  whole number, 0 to 999999999999 (ignored by show)
-- It prints one line starting `OK` or `REFUSED`; a refusal writes nothing.
--
-- One transaction: the city row is locked (rule 10), the stock is changed (a remove stops at 0), one ledger row with reason DEV_ADJUST
-- records the change really made, so the ledger still ends on the stock (rule 11), and the city's state_version goes up, so a running
-- API and the client see a new version at the next request.
\set ON_ERROR_STOP on

BEGIN;

-- Refuse a mode or an amount the rest of the script could not handle.
SELECT (:'mode' IN ('show', 'add', 'remove', 'set')) AS mode_ok,
       (:'mode' = 'show' OR :'amount' ~ '^[0-9]{1,12}$') AS amount_ok,
       (:'mode' = 'show' OR :'item' ~ '^[a-z][a-z0-9_]{0,63}$') AS item_ok \gset

\if :mode_ok
\else
  \echo REFUSED: mode must be show, add, remove or set
  ROLLBACK;
  \quit
\endif
\if :amount_ok
\else
  \echo REFUSED: amount must be a whole number from 0 to 999999999999
  ROLLBACK;
  \quit
\endif
\if :item_ok
\else
  \echo REFUSED: the item is not an item id
  ROLLBACK;
  \quit
\endif

-- Find the player's city and lock its row; '' when the player or the city does not exist.
SELECT coalesce((SELECT c.id::text FROM cities c JOIN players p ON p.id = c.player_id WHERE p.dev_name = :'player' FOR UPDATE OF c), '') AS city_id \gset
SELECT (:'city_id' <> '') AS has_city, (:'mode' = 'show') AS is_show \gset

\if :has_city
\else
  \echo REFUSED: no city for player :player (log in with the client once first)
  ROLLBACK;
  \quit
\endif

\if :is_show
  SELECT item_id AS item, quantity FROM city_inventory WHERE city_id = :'city_id'::uuid ORDER BY item_id;
  \echo OK show
  COMMIT;
  \quit
\endif

-- The stock now, and what it becomes.
SELECT coalesce((SELECT quantity FROM city_inventory WHERE city_id = :'city_id'::uuid AND item_id = :'item'), 0) AS before \gset
SELECT CASE :'mode'
         WHEN 'add' THEN :before + :'amount'::bigint
         WHEN 'remove' THEN greatest(:before - :'amount'::bigint, 0)
         ELSE :'amount'::bigint
       END AS after \gset

INSERT INTO city_inventory (city_id, item_id, quantity) VALUES (:'city_id'::uuid, :'item', :after)
  ON CONFLICT (city_id, item_id) DO UPDATE SET quantity = EXCLUDED.quantity;

-- A change that changes nothing (removing from an empty stock) leaves no ledger row.
INSERT INTO ledger_entries (city_id, item_id, delta, balance_after, reason, ref_type, ref_id, created_at)
  SELECT :'city_id'::uuid, :'item', :after - :before, :after, 'DEV_ADJUST', 'dev-panel', :'mode', now()
  WHERE :after <> :before;

UPDATE cities SET state_version = state_version + 1, updated_at = now() WHERE id = :'city_id'::uuid;

COMMIT;
\echo OK :item :before -> :after
