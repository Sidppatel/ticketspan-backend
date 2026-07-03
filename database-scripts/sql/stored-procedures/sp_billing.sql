-- Developer billing lifecycle: trials, subscriptions, Pay Per Event upgrades,
-- add-ons, and event-level fee overrides. All money amounts land in the
-- billing_charges ledger (source of truth for developer revenue reports).
-- Callers (DeveloperBillingServiceImpl) are developer-gated and write audit
-- logs; these functions hold the state transitions only.
--
-- ponytail: charges are ledger rows only — actual Stripe collection for
-- subscriptions/PPE is deferred; stripe_* id columns exist for when it lands.

-- ============ Trials ============

CREATE OR REPLACE FUNCTION sp_start_trial(p_tenants_id uuid)
RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_id uuid;
BEGIN
    IF EXISTS (SELECT 1 FROM tenant_subscriptions
                WHERE tenants_id = p_tenants_id AND status IN ('trial','active','past_due')) THEN
        RAISE EXCEPTION 'Tenant already has a live subscription or trial.';
    END IF;
    INSERT INTO tenant_subscriptions (tenant_subscriptions_id, tenants_id, tier, status,
        monthly_price_cents, started_at, current_period_end, cancel_at_period_end,
        trial_ends_at, trial_reminder_day_sent, failed_payment_count, created_at, updated_at)
    VALUES (gen_random_uuid(), p_tenants_id, 'professional', 'trial',
        0, now(), now() + interval '14 days', false,
        now() + interval '14 days', 0, 0, now(), now())
    RETURNING tenant_subscriptions_id INTO v_id;
    PERFORM sp_set_tenant_tier(p_tenants_id, 'trial');
    RETURN v_id;
END; $$;

-- Expires overdue trials back to the free tier. Returns count expired.
CREATE OR REPLACE FUNCTION sp_expire_trials()
RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE r record; v_count int := 0;
BEGIN
    FOR r IN SELECT tenant_subscriptions_id, tenants_id FROM tenant_subscriptions
              WHERE status = 'trial' AND trial_ends_at <= now()
    LOOP
        UPDATE tenant_subscriptions SET status = 'expired', updated_at = now()
         WHERE tenant_subscriptions_id = r.tenant_subscriptions_id;
        PERFORM sp_set_tenant_tier(r.tenants_id, 'free');
        v_count := v_count + 1;
    END LOOP;
    RETURN v_count;
END; $$;

-- Trials due a day-10 or day-13 reminder that has not been sent yet.
CREATE OR REPLACE FUNCTION sp_trial_reminders_due()
RETURNS TABLE(tenant_subscriptions_id uuid, tenants_id uuid, reminder_day int, trial_ends_at timestamptz)
LANGUAGE sql STABLE
    SET search_path = public, extensions, pg_catalog
AS $$
    SELECT s.tenant_subscriptions_id, s.tenants_id,
           CASE WHEN now() >= s.trial_ends_at - interval '1 day' THEN 13 ELSE 10 END,
           s.trial_ends_at
      FROM tenant_subscriptions s
     WHERE s.status = 'trial'
       AND ((now() >= s.trial_ends_at - interval '1 day' AND s.trial_reminder_day_sent < 13)
         OR (now() >= s.trial_ends_at - interval '4 days' AND s.trial_reminder_day_sent < 10));
$$;

CREATE OR REPLACE FUNCTION sp_mark_trial_reminder(p_id uuid, p_day int)
RETURNS void LANGUAGE sql
    SET search_path = public, extensions, pg_catalog
AS $$
    UPDATE tenant_subscriptions SET trial_reminder_day_sent = p_day, updated_at = now()
     WHERE tenant_subscriptions_id = p_id;
$$;

-- ============ Subscriptions ============

-- Creates a subscription (also converts a live trial). First month prorated to
-- the 1st of next month; renewals happen on the 1st (sp_renew_subscriptions).
CREATE OR REPLACE FUNCTION sp_create_subscription(p_tenants_id uuid, p_tier text)
RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE t record; v_id uuid; v_period_end timestamptz; v_charge int;
BEGIN
    IF p_tier NOT IN ('starter','professional','business','enterprise') THEN
        RAISE EXCEPTION 'invalid subscription tier: %', p_tier;
    END IF;
    -- A live trial converts; a live paid subscription blocks (use change-tier).
    UPDATE tenant_subscriptions SET status = 'canceled', canceled_at = now(), updated_at = now()
     WHERE tenants_id = p_tenants_id AND status = 'trial';
    IF EXISTS (SELECT 1 FROM tenant_subscriptions
                WHERE tenants_id = p_tenants_id AND status IN ('active','past_due')) THEN
        RAISE EXCEPTION 'Tenant already has an active subscription. Use change-tier instead.';
    END IF;

    SELECT * INTO t FROM app.tier_pricing(p_tier);
    v_period_end := date_trunc('month', now()) + interval '1 month';
    -- Prorate the first month by remaining fraction of the current month.
    v_charge := round(t.monthly_cents *
        extract(epoch FROM (v_period_end - now()))
        / extract(epoch FROM (v_period_end - date_trunc('month', now()))))::int;

    INSERT INTO tenant_subscriptions (tenant_subscriptions_id, tenants_id, tier, status,
        monthly_price_cents, started_at, current_period_end, cancel_at_period_end,
        trial_reminder_day_sent, failed_payment_count, created_at, updated_at)
    VALUES (gen_random_uuid(), p_tenants_id, p_tier, 'active',
        t.monthly_cents, now(), v_period_end, false, 0, 0, now(), now())
    RETURNING tenant_subscriptions_id INTO v_id;

    INSERT INTO billing_charges (billing_charges_id, tenants_id, kind, reference, amount_cents, description, created_at, updated_at)
    VALUES (gen_random_uuid(), p_tenants_id, 'subscription', p_tier, v_charge,
        format('%s subscription (prorated first month)', initcap(p_tier)), now(), now());

    PERFORM sp_set_tenant_tier(p_tenants_id, p_tier);
    RETURN v_id;
END; $$;

-- Upgrade: prorated difference charged now, tier switches immediately.
-- Downgrade: takes effect at period end via pending_tier. Returns old tier.
CREATE OR REPLACE FUNCTION sp_change_subscription_tier(p_tenants_id uuid, p_tier text)
RETURNS text LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE s record; t record; v_delta int;
BEGIN
    IF p_tier NOT IN ('starter','professional','business','enterprise') THEN
        RAISE EXCEPTION 'invalid subscription tier: %', p_tier;
    END IF;
    SELECT * INTO s FROM tenant_subscriptions
     WHERE tenants_id = p_tenants_id AND status IN ('active','past_due');
    IF NOT FOUND THEN
        RAISE EXCEPTION 'No active subscription for tenant.';
    END IF;
    IF s.tier = p_tier THEN
        RAISE EXCEPTION 'Tenant is already on the % tier.', p_tier;
    END IF;

    SELECT * INTO t FROM app.tier_pricing(p_tier);
    IF t.monthly_cents > s.monthly_price_cents THEN
        -- Upgrade now: charge the prorated difference for the rest of the period.
        v_delta := round((t.monthly_cents - s.monthly_price_cents) *
            GREATEST(extract(epoch FROM (s.current_period_end - now())), 0)
            / extract(epoch FROM interval '1 month'))::int;
        UPDATE tenant_subscriptions SET tier = p_tier, monthly_price_cents = t.monthly_cents,
            pending_tier = NULL, updated_at = now()
         WHERE tenant_subscriptions_id = s.tenant_subscriptions_id;
        INSERT INTO billing_charges (billing_charges_id, tenants_id, kind, reference, amount_cents, description, created_at, updated_at)
        VALUES (gen_random_uuid(), p_tenants_id, 'proration', p_tier, v_delta,
            format('Upgrade %s → %s (prorated)', s.tier, p_tier), now(), now());
        PERFORM sp_set_tenant_tier(p_tenants_id, p_tier);
    ELSE
        -- Downgrade at period end; features remain until then. No refunds.
        UPDATE tenant_subscriptions SET pending_tier = p_tier, updated_at = now()
         WHERE tenant_subscriptions_id = s.tenant_subscriptions_id;
    END IF;
    RETURN s.tier;
END; $$;

CREATE OR REPLACE FUNCTION sp_cancel_subscription(p_tenants_id uuid, p_at_period_end bool)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE s record;
BEGIN
    SELECT * INTO s FROM tenant_subscriptions
     WHERE tenants_id = p_tenants_id AND status IN ('trial','active','past_due');
    IF NOT FOUND THEN
        RAISE EXCEPTION 'No live subscription for tenant.';
    END IF;
    IF p_at_period_end AND s.status <> 'trial' THEN
        UPDATE tenant_subscriptions SET cancel_at_period_end = true, pending_tier = NULL, updated_at = now()
         WHERE tenant_subscriptions_id = s.tenant_subscriptions_id;
    ELSE
        UPDATE tenant_subscriptions SET status = 'canceled', canceled_at = now(), updated_at = now()
         WHERE tenant_subscriptions_id = s.tenant_subscriptions_id;
        PERFORM sp_set_tenant_tier(p_tenants_id, 'free');
    END IF;
END; $$;

-- Rolls all subscriptions past their period end: cancel-at-period-end and
-- pending downgrades apply, everything else renews with a fresh monthly charge.
-- Run by BillingWorker. Returns rows processed.
CREATE OR REPLACE FUNCTION sp_renew_subscriptions()
RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE r record; t record; v_count int := 0;
BEGIN
    FOR r IN SELECT * FROM tenant_subscriptions
              WHERE status IN ('active','past_due') AND current_period_end <= now()
    LOOP
        IF r.cancel_at_period_end THEN
            UPDATE tenant_subscriptions SET status = 'canceled', canceled_at = now(), updated_at = now()
             WHERE tenant_subscriptions_id = r.tenant_subscriptions_id;
            PERFORM sp_set_tenant_tier(r.tenants_id, 'free');
        ELSE
            IF r.pending_tier IS NOT NULL THEN
                SELECT * INTO t FROM app.tier_pricing(r.pending_tier);
                UPDATE tenant_subscriptions SET tier = r.pending_tier,
                    monthly_price_cents = t.monthly_cents, pending_tier = NULL
                 WHERE tenant_subscriptions_id = r.tenant_subscriptions_id;
                PERFORM sp_set_tenant_tier(r.tenants_id, r.pending_tier);
                r.tier := r.pending_tier; r.monthly_price_cents := t.monthly_cents;
            END IF;
            UPDATE tenant_subscriptions SET
                current_period_end = current_period_end + interval '1 month',
                status = 'active', updated_at = now()
             WHERE tenant_subscriptions_id = r.tenant_subscriptions_id;
            INSERT INTO billing_charges (billing_charges_id, tenants_id, kind, reference, amount_cents, description, created_at, updated_at)
            VALUES (gen_random_uuid(), r.tenants_id, 'subscription', r.tier, r.monthly_price_cents,
                format('%s subscription renewal', initcap(r.tier)), now(), now());
        END IF;
        v_count := v_count + 1;
    END LOOP;
    RETURN v_count;
END; $$;

-- ============ Pay Per Event ============

CREATE OR REPLACE FUNCTION sp_activate_event_upgrade(p_events_id uuid, p_tier text)
RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE t record; v_tenant uuid; v_id uuid; v_formula uuid;
BEGIN
    SELECT * INTO t FROM app.event_tier_pricing(p_tier);
    IF NOT FOUND THEN
        RAISE EXCEPTION 'invalid Pay Per Event tier: %', p_tier;
    END IF;
    SELECT tenants_id INTO v_tenant FROM events WHERE events_id = p_events_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Event not found.';
    END IF;
    IF EXISTS (SELECT 1 FROM event_upgrades WHERE events_id = p_events_id AND status = 'active') THEN
        RAISE EXCEPTION 'Event already has an active Pay Per Event upgrade. Use change-tier by cancelling first.';
    END IF;

    INSERT INTO event_upgrades (event_upgrades_id, events_id, tenants_id, tier, status,
        price_cents, sms_credits, custom_domain_limit, refunded_cents, created_at, updated_at)
    VALUES (gen_random_uuid(), p_events_id, v_tenant, p_tier, 'active',
        t.price_cents, t.sms_credits, t.custom_domain_limit, 0, now(), now())
    RETURNING event_upgrades_id INTO v_id;

    INSERT INTO billing_charges (billing_charges_id, tenants_id, kind, reference, events_id, amount_cents, description, created_at, updated_at)
    VALUES (gen_random_uuid(), v_tenant, 'pay_per_event', p_tier, p_events_id, t.price_cents,
        format('Pay Per Event: %s', replace(initcap(replace(p_tier, '_', ' ')), ' Event', ' Event')), now(), now());

    -- Point the event's fee override at the PPE tier formula (beats tenant default).
    v_formula := app.ensure_tier_formula('ppe:' || p_tier, t.percent_bps, t.flat_cents, t.min_fee_cents);
    UPDATE events SET fee_formulas_id = v_formula, fee_override_expires_at = NULL, updated_at = now()
     WHERE events_id = p_events_id;
    PERFORM app.recompute_event_cached_fees(p_events_id);
    RETURN v_id;
END; $$;

CREATE OR REPLACE FUNCTION sp_cancel_event_upgrade(p_events_id uuid, p_refund_cents int)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE u record;
BEGIN
    SELECT * INTO u FROM event_upgrades WHERE events_id = p_events_id AND status = 'active';
    IF NOT FOUND THEN
        RAISE EXCEPTION 'No active Pay Per Event upgrade on this event.';
    END IF;
    UPDATE event_upgrades SET
        status = CASE WHEN COALESCE(p_refund_cents, 0) > 0 THEN 'refunded' ELSE 'canceled' END,
        refunded_cents = COALESCE(p_refund_cents, 0), canceled_at = now(), updated_at = now()
     WHERE event_upgrades_id = u.event_upgrades_id;
    IF COALESCE(p_refund_cents, 0) > 0 THEN
        INSERT INTO billing_charges (billing_charges_id, tenants_id, kind, reference, events_id, amount_cents, description, created_at, updated_at)
        VALUES (gen_random_uuid(), u.tenants_id, 'refund', u.tier, p_events_id, -p_refund_cents,
            'Pay Per Event refund', now(), now());
    END IF;
    UPDATE events SET fee_formulas_id = NULL, fee_override_expires_at = NULL, updated_at = now()
     WHERE events_id = p_events_id;
    PERFORM app.recompute_event_cached_fees(p_events_id);
END; $$;

-- ============ Add-ons ============

CREATE OR REPLACE FUNCTION sp_provision_addon(p_tenants_id uuid, p_type text, p_billing_period text, p_quantity int)
RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE a record; v_id uuid; v_qty int := GREATEST(COALESCE(p_quantity, 1), 1);
        v_price int; v_period_end timestamptz;
BEGIN
    SELECT * INTO a FROM app.addon_pricing(p_type);
    IF NOT FOUND THEN
        RAISE EXCEPTION 'invalid add-on type: %', p_type;
    END IF;
    IF p_billing_period NOT IN ('monthly','annual') THEN
        RAISE EXCEPTION 'invalid billing period: %', p_billing_period;
    END IF;
    v_price := CASE WHEN p_billing_period = 'annual' THEN a.annual_cents ELSE a.monthly_cents END * v_qty;
    v_period_end := now() + CASE WHEN p_billing_period = 'annual' THEN interval '1 year' ELSE interval '1 month' END;

    INSERT INTO tenant_addons (tenant_addons_id, tenants_id, type, billing_period, quantity,
        price_cents, setup_fee_cents, status, current_period_end, usage_count, created_at, updated_at)
    VALUES (gen_random_uuid(), p_tenants_id, p_type, p_billing_period, v_qty,
        v_price, a.setup_fee_cents, 'active', v_period_end, 0, now(), now())
    RETURNING tenant_addons_id INTO v_id;

    INSERT INTO billing_charges (billing_charges_id, tenants_id, kind, reference, amount_cents, description, created_at, updated_at)
    VALUES (gen_random_uuid(), p_tenants_id, 'addon', p_type, v_price,
        format('%s add-on (%s)', replace(initcap(replace(p_type, '_', ' ')), 'Sms', 'SMS'), p_billing_period), now(), now());
    IF a.setup_fee_cents > 0 THEN
        INSERT INTO billing_charges (billing_charges_id, tenants_id, kind, reference, amount_cents, description, created_at, updated_at)
        VALUES (gen_random_uuid(), p_tenants_id, 'setup_fee', p_type, a.setup_fee_cents,
            'Custom domain one-time setup', now(), now());
    END IF;
    RETURN v_id;
END; $$;

CREATE OR REPLACE FUNCTION sp_cancel_addon(p_tenant_addons_id uuid, p_refund_cents int)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE a record;
BEGIN
    SELECT * INTO a FROM tenant_addons WHERE tenant_addons_id = p_tenant_addons_id AND status = 'active';
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Add-on not found or already canceled.';
    END IF;
    UPDATE tenant_addons SET status = 'canceled', canceled_at = now(), updated_at = now()
     WHERE tenant_addons_id = p_tenant_addons_id;
    IF COALESCE(p_refund_cents, 0) > 0 THEN
        INSERT INTO billing_charges (billing_charges_id, tenants_id, kind, reference, amount_cents, description, created_at, updated_at)
        VALUES (gen_random_uuid(), a.tenants_id, 'refund', a.type, -p_refund_cents,
            'Add-on prorated refund', now(), now());
    END IF;
END; $$;

-- Renews add-ons past their period end with a fresh charge. Returns count.
CREATE OR REPLACE FUNCTION sp_renew_addons()
RETURNS int LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE r record; v_count int := 0;
BEGIN
    FOR r IN SELECT * FROM tenant_addons WHERE status = 'active' AND current_period_end <= now()
    LOOP
        UPDATE tenant_addons SET
            current_period_end = current_period_end +
                CASE WHEN r.billing_period = 'annual' THEN interval '1 year' ELSE interval '1 month' END,
            updated_at = now()
         WHERE tenant_addons_id = r.tenant_addons_id;
        INSERT INTO billing_charges (billing_charges_id, tenants_id, kind, reference, amount_cents, description, created_at, updated_at)
        VALUES (gen_random_uuid(), r.tenants_id, 'addon', r.type, r.price_cents,
            format('%s add-on renewal (%s)', replace(initcap(replace(r.type, '_', ' ')), 'Sms', 'SMS'), r.billing_period), now(), now());
        v_count := v_count + 1;
    END LOOP;
    RETURN v_count;
END; $$;

-- ============ Event fee overrides ============

-- Re-resolves cached platform_fee_cents on an event's sellables that have no
-- explicit price-level formula (those keep their own). Cached values are
-- display-only; checkout always computes live via app.price_breakdown.
CREATE OR REPLACE FUNCTION app.recompute_event_cached_fees(p_events_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_tenant uuid; v_formula uuid;
BEGIN
    SELECT tenants_id INTO v_tenant FROM events WHERE events_id = p_events_id;
    v_formula := app.resolve_fee_formula(NULL, p_events_id, v_tenant);
    UPDATE event_ticket_types
       SET platform_fee_cents = app.compute_fee(price_cents, COALESCE(fee_formulas_id, v_formula)), updated_at = now()
     WHERE events_id = p_events_id;
    UPDATE event_tables
       SET platform_fee_cents = app.compute_fee(price_cents, COALESCE(fee_formulas_id, v_formula)), updated_at = now()
     WHERE events_id = p_events_id;
END; $$;

-- Manual event-level fee override (non-profits, promos). Creates/updates a
-- dedicated 'override:event:<id>' formula and points the event at it. Silent
-- to tenants; audit is written by the calling service with the required reason.
CREATE OR REPLACE FUNCTION sp_set_event_fee_override(
    p_events_id uuid, p_percent_bps int, p_flat_cents int,
    p_min_fee_cents int, p_max_fee_cents int, p_expires_at timestamptz
) RETURNS uuid LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
DECLARE v_formula uuid; v_name text := 'override:event:' || p_events_id;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM events WHERE events_id = p_events_id) THEN
        RAISE EXCEPTION 'Event not found.';
    END IF;
    SELECT fee_formulas_id INTO v_formula FROM fee_formulas WHERE name = v_name;
    IF FOUND THEN
        UPDATE fee_formulas SET percent_bps = COALESCE(p_percent_bps, 0),
            flat_cents = COALESCE(p_flat_cents, 0), min_fee_cents = p_min_fee_cents,
            max_fee_cents = p_max_fee_cents, is_active = true, updated_at = now()
         WHERE fee_formulas_id = v_formula;
    ELSE
        INSERT INTO fee_formulas (fee_formulas_id, name, percent_bps, flat_cents,
            min_fee_cents, max_fee_cents, is_active, created_at, updated_at)
        VALUES (gen_random_uuid(), v_name, COALESCE(p_percent_bps, 0), COALESCE(p_flat_cents, 0),
            p_min_fee_cents, p_max_fee_cents, true, now(), now())
        RETURNING fee_formulas_id INTO v_formula;
    END IF;
    UPDATE events SET fee_formulas_id = v_formula, fee_override_expires_at = p_expires_at, updated_at = now()
     WHERE events_id = p_events_id;
    PERFORM app.recompute_event_cached_fees(p_events_id);
    RETURN v_formula;
END; $$;

CREATE OR REPLACE FUNCTION sp_clear_event_fee_override(p_events_id uuid)
RETURNS void LANGUAGE plpgsql
    SET search_path = public, extensions, pg_catalog
AS $$
BEGIN
    UPDATE events SET fee_formulas_id = NULL, fee_override_expires_at = NULL, updated_at = now()
     WHERE events_id = p_events_id;
    DELETE FROM fee_formulas WHERE name = 'override:event:' || p_events_id;
    PERFORM app.recompute_event_cached_fees(p_events_id);
END; $$;
