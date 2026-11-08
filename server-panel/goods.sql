-- Server panel: adds, removes or sets the coins of a test player's purse or one good in the store of one of their buildings, or shows the
-- purse and every store (CR-0006; CR-0008 decision 35: goods live in building stores, coins in the purse).
-- A developer tool for the local dev database, run by `panel.sh goods ...` through psql. It is not part of the game: no endpoint and no
-- client code reach it (core-instructions/02-principles.md rule 5 is about those). panel.sh checks that the item is in the content file and
-- passes the content's currency.
--
-- Run:  psql -X -q -v ON_ERROR_STOP=1 -v player=devtest -v mode=add -v item=planks -v amount=50 -v building=3 -v currency=coins -f goods.sql
--   player    the dev name of the player; panel.sh passes the one the Unity client logs in as, or $PLAYER (CR-0006-T2)
--   mode      show | add | remove | set
--   item      an item id of the content (ignored by show)
--   amount    whole number, 0 to 999999999999 (ignored by show)
--   building  the id of the building whose store gets the good; ignored for the currency and by show (pass 0)
--   currency  the content's currency item id (its coins live in the purse, cities.purse)
-- It prints one line starting `OK` or `REFUSED`; a refusal writes nothing. The script does not check that the building's level stores the
-- good: a store row of a good it doesn't hold stays until a trip takes it.
--
-- One transaction: the city row is locked (rule 10), the purse or the store is changed (a remove stops at 0), one ledger row with reason
-- DEV_ADJUST records the change really made with the city's total after it (purse; or every store and trip load), so the ledger still ends
-- on the totals (rule 11), and the city's state_version goes up, so a running API and the client see a new version at the next request.
\set ON_ERROR_STOP on

BEGIN;

-- Refuse a mode, an amount, an item or a building the rest of the script could not handle.
SELECT (:'mode' IN ('show', 'add', 'remove', 'set')) AS mode_ok,
       (:'mode' = 'show' OR :'amount' ~ '^[0-9]{1,12}$') AS amount_ok,
       (:'mode' = 'show' OR :'item' ~ '^[a-z][a-z0-9_]{0,63}$') AS item_ok,
       (:'currency' ~ '^[a-z][a-z0-9_]{0,63}$') AS currency_ok,
       (:'mode' = 'show' OR :'item' = :'currency' OR :'building' ~ '^[1-9][0-9]{0,8}$') AS building_ok \gset

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
\if :currency_ok
\else
  \echo REFUSED: the currency is not an item id
  ROLLBACK;
  \quit
\endif
\if :building_ok
\else
  \echo REFUSED: a good needs the id of the building whose store gets it
  ROLLBACK;
  \quit
\endif

-- Find the player's city and lock its row; '' when the player or the city does not exist.
SELECT coalesce((SELECT c.id::text FROM cities c JOIN players p ON p.id = c.player_id WHERE p.dev_name = :'player' FOR UPDATE OF c), '') AS city_id \gset
SELECT (:'city_id' <> '') AS has_city, (:'mode' = 'show') AS is_show, (:'item' = :'currency') AS is_currency \gset

\if :has_city
\else
  \echo REFUSED: no city for player :player (log in with the client once first)
  ROLLBACK;
  \quit
\endif

\if :is_show
  SELECT 'purse' AS place, :'currency' AS item, purse AS quantity FROM cities WHERE id = :'city_id'::uuid
  UNION ALL
  SELECT 'building ' || s.building_id || ' ' || b.type_id, s.item_id, s.quantity
    FROM city_stores s JOIN city_buildings b ON b.city_id = s.city_id AND b.building_id = s.building_id
    WHERE s.city_id = :'city_id'::uuid AND s.quantity > 0;
  \echo OK show
  COMMIT;
  \quit
\endif

\if :is_currency
  -- The coins: the purse.
  SELECT purse AS before FROM cities WHERE id = :'city_id'::uuid \gset
\else
  -- A good: the building must be the player's.
  SELECT EXISTS (SELECT 1 FROM city_buildings WHERE city_id = :'city_id'::uuid AND building_id = :'building'::int) AS has_building \gset
  \if :has_building
  \else
    \echo REFUSED: the city has no building :building
    ROLLBACK;
    \quit
  \endif
  SELECT coalesce((SELECT quantity FROM city_stores
                   WHERE city_id = :'city_id'::uuid AND building_id = :'building'::int AND item_id = :'item'), 0) AS before \gset
\endif

SELECT CASE :'mode'
         WHEN 'add' THEN :before + :'amount'::bigint
         WHEN 'remove' THEN greatest(:before - :'amount'::bigint, 0)
         ELSE :'amount'::bigint
       END AS after \gset

\if :is_currency
  UPDATE cities SET purse = :after WHERE id = :'city_id'::uuid;
  SELECT :after AS total \gset
\else
  INSERT INTO city_stores (city_id, building_id, item_id, quantity) VALUES (:'city_id'::uuid, :'building'::int, :'item', :after)
    ON CONFLICT (city_id, building_id, item_id) DO UPDATE SET quantity = EXCLUDED.quantity;
  SELECT coalesce((SELECT sum(quantity) FROM city_stores WHERE city_id = :'city_id'::uuid AND item_id = :'item'), 0)
       + coalesce((SELECT sum(quantity) FROM city_trips WHERE city_id = :'city_id'::uuid AND item_id = :'item'), 0) AS total \gset
\endif

-- A change that changes nothing (removing from an empty store) leaves no ledger row.
INSERT INTO ledger_entries (city_id, item_id, delta, balance_after, reason, ref_type, ref_id, created_at)
  SELECT :'city_id'::uuid, :'item', :after - :before, :total, 'DEV_ADJUST', 'dev-panel', :'mode', now()
  WHERE :after <> :before;

UPDATE cities SET state_version = state_version + 1, updated_at = now() WHERE id = :'city_id'::uuid;

COMMIT;
\echo OK :item :before -> :after
