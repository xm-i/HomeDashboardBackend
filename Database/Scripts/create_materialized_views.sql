-- ============================================================
-- DailyAssetProgresses マテリアライズドビュー作成スクリプト
-- ============================================================
-- 実行タイミング:
--   API サーバー初回起動後（EnsureCreated でテーブル群が作成された後）に
--   PostgreSQL に対して本スクリプトを実行する。
-- ============================================================

-- マテリアライズドビューを作成
CREATE MATERIALIZED VIEW "DailyAssetProgresses" AS
WITH RECURSIVE dates("Date") AS (
    SELECT (SELECT MIN("Date") FROM "InvestmentProductRates")::date
    UNION ALL
    SELECT ("Date" + INTERVAL '1 day')::date
    FROM dates
    WHERE "Date" < (SELECT MAX("Date") FROM "InvestmentProductRates")
)
SELECT
    sub1."Date" AS "Date",
    sub1."InvestmentProductId" AS "InvestmentProductId",
    sub1."Rate" AS "Rate",
    sub1."Amount" AS "Amount",
    sub1."AverageRate" AS "AverageRate",
    sub1."CurrencyRate" AS "CurrencyRate"
FROM (
    SELECT
        ip."InvestmentProductId" AS "InvestmentProductId",
        ip."Name" AS "ProductName",
        ip."Category" AS "Category",
        ip."Type" AS "Type",
        ip."InvestmentCurrencyUnitId" AS "InvestmentCurrencyUnitId",
        icu."Name" AS "CurrencyName",
        dates."Date" AS "Date",
        (
            SELECT SUM(ipa."Amount")
            FROM "InvestmentProductAmounts" ipa
            WHERE ip."InvestmentProductId" = ipa."InvestmentProductId"
              AND ipa."Date" <= dates."Date"
        ) AS "Amount",
        (
            SELECT
                CASE WHEN SUM(ipa."Amount") = 0 THEN 0
                ELSE SUM(ipa."Amount" * ipa."Price") / SUM(ipa."Amount") END
            FROM "InvestmentProductAmounts" ipa
            WHERE ip."InvestmentProductId" = ipa."InvestmentProductId"
              AND ipa."Date" <= dates."Date"
        ) AS "AverageRate",
        COALESCE(
            ipr."Value",
            (
                SELECT "Value"
                FROM "InvestmentProductRates"
                WHERE "InvestmentProductId" = ip."InvestmentProductId"
                  AND "Date" = (
                      SELECT MAX("Date")
                      FROM "InvestmentProductRates"
                      WHERE "InvestmentProductId" = ip."InvestmentProductId"
                        AND "Date" <= dates."Date"
                  )
            )
        ) AS "Rate",
        COALESCE(
            (
                SELECT icr."Value"
                FROM "InvestmentCurrencyRates" icr
                WHERE icr."InvestmentCurrencyUnitId" = icu."Id"
                  AND "Date" = (
                      SELECT MAX("Date")
                      FROM "InvestmentCurrencyRates"
                      WHERE "InvestmentCurrencyUnitId" = icr."InvestmentCurrencyUnitId"
                        AND "Date" <= dates."Date"
                  )
            ),
            1
        ) AS "CurrencyRate"
    FROM
        dates
    INNER JOIN "InvestmentProducts" ip ON TRUE
    LEFT JOIN "InvestmentProductRates" ipr ON
        ip."InvestmentProductId" = ipr."InvestmentProductId"
        AND dates."Date" = ipr."Date"
    INNER JOIN "InvestmentCurrencyUnits" icu ON
        ip."InvestmentCurrencyUnitId" = icu."Id"
) sub1
WHERE EXISTS (
    SELECT 1
    FROM "InvestmentProductAmounts" ipa
    WHERE sub1."InvestmentProductId" = ipa."InvestmentProductId"
      AND ipa."Date" <= sub1."Date"
);

-- REFRESH MATERIALIZED VIEW CONCURRENTLY に必要なユニークインデックスを作成
CREATE UNIQUE INDEX "IX_DailyAssetProgresses_Date_ProductId"
    ON "DailyAssetProgresses" ("Date", "InvestmentProductId");