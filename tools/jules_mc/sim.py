import argparse
import random
import numpy as np
import csv
import os
import math
import sys

# Seed for reproducibility
SEED = 42

# Ensure results directory exists
os.makedirs("tools/jules_mc/results", exist_ok=True)

# Game Constants
# From GameEconomySettings.cs / EkonomiAyarlari.asset
BASE_RENT_BY_P = [290, 650, 1140, 1630]
RENT_GROWTH_MULT = 1.20
RENT_INTERVAL_DAYS = 4
GRACE_PAYMENT_PERCENT = 0.8
UPGRADE_RENT_RESERVE_FRACTION = 1.0
DAY_END_GRACE_SECONDS = 30.0

REWARD_PER_BOX_BY_P = [50, 55, 70, 88]
PENALTY_PER_BOX = 40
HANGAR_STAY_DURATION_BY_P = [120, 60, 40, 30]
TRUCK_CARGO_MIN_BY_P = [1, 2, 2, 2]
TRUCK_CARGO_MAX_EXC_BY_P = [3, 4, 5, 6]
BOX_DROP_MONEY_PENALTY = 5

CUSTOMER_QUOTAS = [
    [4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6],    # 1P
    [7, 7, 7, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12],  # 2P
    [8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 12, 12, 12, 13], # 3P
    [8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 12, 12, 12, 13]  # 4P
]
CUSTOMER_ARRIVAL_INTERVAL = [44.0, 22.0, 21.0, 21.0]

# Prestige settings
STARTING_PRESTIGE = 12.0
MAX_PRESTIGE = 100.0
CUSTOMER_LOST_PRESTIGE_PENALTY = -0.4
CUSTOMER_MISSED_QUOTA_PRESTIGE_PENALTY = -0.2
CUSTOMER_SERVED_PRESTIGE_BONUS = 0.4
WRONG_PRODUCT_PRESTIGE_PENALTY = -0.2
BOX_DROP_PRESTIGE_PENALTY = -0.04
WRONG_DELIVERY_PRESTIGE_PENALTY = -0.16

# DifficultyManager.cs
BASE_STARTING_MONEY = 500
MONEY_MULTIPLIER_PER_PLAYER = 1.2

# DayCycleManager.cs
MAX_DAYS = 16
BASE_DAY_DURATION = 200.0
DAILY_DURATION_INCREASE = 10.0
DYNAMIC_DURATION_START_DAY = 3

# PostRentFeatureUnlocks.cs
RETURN_UNLOCK_DAY = 5
RETURN_MODE_CHANCE = 0.25

# DraftPool.cs
T2_UNLOCK_DAY = 5
T3_UNLOCK_DAY = 9
OFFER_COUNT = 3

# Upgrade definitions
UPGRADES = [
    # Tier 1
    {"id": "delivery_bonus", "tier": 1, "cost": 100, "type": "reward_mult", "val": 1.1},
    {"id": "speed_shoes", "tier": 1, "cost": 150, "type": "speed_mult", "val": 1.15},
    {"id": "stamina_drink", "tier": 1, "cost": 80, "type": "stamina", "val": 0},
    # Tier 2
    {"id": "big_truck", "tier": 2, "cost": 250, "type": "truck_cargo", "val": 1},
    {"id": "phone_line", "tier": 2, "cost": 200, "type": "customer", "val": 0},
    {"id": "better_shoes", "tier": 2, "cost": 300, "type": "speed_mult", "val": 1.3},
    # Tier 3
    {"id": "automated_packer", "tier": 3, "cost": 600, "type": "speed_mult", "val": 1.5},
    {"id": "emergency_brake", "tier": 3, "cost": 400, "type": "emergency", "val": 0}
]

EVENTS = {
    "EventBusyDay": {"type": "neg", "cust_mult": 1.35, "pat_mult": 0.85},
    "EventDeliveryBonus": {"type": "pos", "reward_mult": 1.20},
    "EventAngryCustomers": {"type": "neg", "pat_mult": 0.60, "cust_mult": 1.10},
    "EventRelaxedDay": {"type": "pos", "pat_mult": 1.30},
    "EventSlowLogistics": {"type": "neg", "truck_speed_mult": 0.50, "reward_mult": 0.92},
    "EventExpressCargo": {"type": "pos", "truck_speed_mult": 1.30, "reward_mult": 1.08},
    "EventHeavyBoxes": {"type": "neg", "speed_mult": 0.85},
    "EventGoldenBoxDay": {"type": "pos", "reward_mult": 1.15, "cust_mult": 1.15, "truck_speed_mult": 1.20, "speed_mult": 1.08},
    "EventOpportunityDay": {"type": "pos", "upgrade_cost_mult": 0.80},
    "EventFatigueProblem": {"type": "neg", "speed_mult": 0.70},
    "EventVipService": {"type": "pos", "reward_mult": 1.12},
    "EventSurpriseAudit": {"type": "neg", "penalty_mult": 2.0},
    "EventRainyDay": {"type": "neg", "cust_mult": 0.80},
    "EventMarketingDay": {"type": "neg", "cust_mult": 1.20, "reward_mult": 0.70},
    "EventCustomerSupport": {"type": "pos"},
    "EventFestivalDay": {"type": "pos", "bonus_min": 100, "bonus_max": 300}
}
POSITIVE_EVENTS = [k for k, v in EVENTS.items() if v["type"] == "pos"]
NEGATIVE_EVENTS = [k for k, v in EVENTS.items() if v["type"] == "neg"]
ALL_EVENTS = list(EVENTS.keys())

def generate_events(seed, base_day=0, what_if=False):
    rng = random.Random(seed)
    random_event_days = []
    events_by_day = {}

    if what_if:
        available_events = list(ALL_EVENTS)
        rng.shuffle(available_events)
        for day in range(1, 17):
            if day % 4 == 0: continue
            if not available_events: break
            events_by_day[day] = available_events.pop()
            random_event_days.append(day)
        return events_by_day

    current_day = base_day + 3
    max_day = base_day + 100
    event_count = 0

    while current_day < max_day:
        current_day += rng.randint(1, 2)
        if current_day in random_event_days: continue
        if current_day % 4 == 0: continue

        random_event_days.append(current_day)

        if event_count < 2:
            ev = rng.choice(POSITIVE_EVENTS)
        elif event_count == 2:
            ev = rng.choice(NEGATIVE_EVENTS)
        else:
            ev = rng.choice(ALL_EVENTS)

        events_by_day[current_day] = ev
        event_count += 1

    return events_by_day

class SimulationState:
    def __init__(self, p_count, profile, strategy, seed, what_if):
        self.p_count = p_count
        self.profile = profile
        self.strategy = strategy
        self.seed = seed
        self.rng = random.Random(seed)
        self.what_if = what_if

        money_mult = 1.0
        for _ in range(1, p_count):
            money_mult *= MONEY_MULTIPLIER_PER_PLAYER
        self.money = round(BASE_STARTING_MONEY * money_mult)
        self.prestige = STARTING_PRESTIGE

        self.day = 1
        self.game_over = False
        self.bankruptcy = False
        self.win = False
        self.rent_cycle = 0
        self.upgrades = []

        self.money_at_day = {3: 0, 5: 0, 8: 0, 12: 0}
        self.rent_day_bankrupt = None

        self.events_by_day = generate_events(seed, what_if=what_if)

    def get_rent(self):
        base_rent = BASE_RENT_BY_P[self.p_count - 1]
        return int(base_rent * (RENT_GROWTH_MULT ** self.rent_cycle))

    def get_reward_per_box(self):
        return REWARD_PER_BOX_BY_P[self.p_count - 1]

    def get_daily_quota(self, day):
        return CUSTOMER_QUOTAS[self.p_count - 1][min(day - 1, 15)]

    def get_day_duration(self, day):
        if day <= DYNAMIC_DURATION_START_DAY:
            return BASE_DAY_DURATION
        return BASE_DAY_DURATION + ((day - DYNAMIC_DURATION_START_DAY) * DAILY_DURATION_INCREASE)

    def modify_money(self, amount):
        self.money += amount
        if self.money < 0:
            self.money = 0

    def modify_prestige(self, amount):
        self.prestige += amount
        if self.prestige > MAX_PRESTIGE:
            self.prestige = MAX_PRESTIGE
        if self.prestige <= 0:
            self.game_over = True
            self.bankruptcy = False

    def process_rent(self):
        if self.day % RENT_INTERVAL_DAYS != 0:
            return

        rent_amount = self.get_rent()
        if self.money >= rent_amount:
            self.modify_money(-rent_amount)
        else:
            if self.rent_cycle == 0:
                grace_payment = int(self.money * GRACE_PAYMENT_PERCENT)
                self.modify_money(-grace_payment)
            else:
                self.game_over = True
                self.bankruptcy = True
                self.rent_day_bankrupt = self.day

        self.rent_cycle += 1

PROFILES = {
    "weak": {"sec_per_box": 20.0, "error_rate": 0.10},
    "average": {"sec_per_box": 12.0, "error_rate": 0.05},
    "good": {"sec_per_box": 8.0, "error_rate": 0.02}
}

STRATEGIES = ["never_buy", "greedy", "sensible"]

def run_simulation(p_count, profile_name, strategy, seed, what_if, speed_mod=1.0):
    state = SimulationState(p_count, profile_name, strategy, seed, what_if)

    speed_mult = 1.0 * speed_mod
    reward_mult = 1.0
    stamina_regen = 1.0
    has_emergency_brake = False

    while state.day <= MAX_DAYS and not state.game_over:
        daily_ev = state.events_by_day.get(state.day)
        ev_mods = {"speed_mult": 1.0, "reward_mult": 1.0, "penalty_mult": 1.0, "cust_mult": 1.0, "pat_mult": 1.0, "truck_speed_mult": 1.0}
        if daily_ev:
            ev_data = EVENTS[daily_ev]
            for k, v in ev_data.items():
                if k in ev_mods:
                    ev_mods[k] = v

        if daily_ev == "EventFestivalDay":
            bonus = state.rng.randint(EVENTS["EventFestivalDay"]["bonus_min"], EVENTS["EventFestivalDay"]["bonus_max"])
            state.modify_money(bonus)

        base_quota = state.get_daily_quota(state.day)
        actual_quota = int(round(base_quota * ev_mods["cust_mult"]))

        day_duration = state.get_day_duration(state.day)
        prof = PROFILES[profile_name]
        actual_speed_mult = speed_mult * ev_mods["speed_mult"]
        time_per_box = prof["sec_per_box"] / actual_speed_mult
        time_per_box /= ev_mods["truck_speed_mult"]

        total_time_available = day_duration * p_count
        boxes_processed = int(total_time_available / time_per_box)

        reward_per_box = state.get_reward_per_box()
        actual_reward = int(round(reward_per_box * reward_mult * ev_mods["reward_mult"]))
        actual_penalty = PENALTY_PER_BOX * ev_mods["penalty_mult"]

        err_rate = prof["error_rate"]

        success_boxes = 0
        dropped_boxes = 0
        wrong_deliv = 0

        for _ in range(boxes_processed):
            if state.rng.random() < err_rate:
                if state.rng.random() < 0.5:
                    dropped_boxes += 1
                else:
                    wrong_deliv += 1
            else:
                success_boxes += 1

        state.modify_money(success_boxes * actual_reward)

        for _ in range(dropped_boxes):
            state.modify_money(-int(round(BOX_DROP_MONEY_PENALTY * ev_mods["penalty_mult"])))
            state.modify_prestige(BOX_DROP_PRESTIGE_PENALTY * ev_mods["penalty_mult"])
        for _ in range(wrong_deliv):
            state.modify_money(-int(round(actual_penalty)))
            state.modify_prestige(WRONG_DELIVERY_PRESTIGE_PENALTY * ev_mods["penalty_mult"])

        boxes_needed = actual_quota * 2

        if success_boxes >= boxes_needed:
            for _ in range(actual_quota):
                state.modify_prestige(CUSTOMER_SERVED_PRESTIGE_BONUS)
        else:
            served = success_boxes // 2
            missed = actual_quota - served

            for _ in range(served):
                state.modify_prestige(CUSTOMER_SERVED_PRESTIGE_BONUS)

            for _ in range(missed):
                state.modify_prestige(CUSTOMER_LOST_PRESTIGE_PENALTY * ev_mods["penalty_mult"])

        if state.game_over:
            break

        if strategy != "never_buy":
            unlocked_tier = 3 if state.day >= T3_UNLOCK_DAY else (2 if state.day >= T2_UNLOCK_DAY else 1)
            eligible = [u for u in UPGRADES if u["tier"] <= unlocked_tier]

            if len(eligible) >= 3:
                offers = state.rng.sample(eligible, 3)
            else:
                offers = eligible

            offers.sort(key=lambda x: x["cost"])

            next_rent = state.get_rent() if state.day % RENT_INTERVAL_DAYS != 0 or state.day == 16 else int(BASE_RENT_BY_P[p_count-1] * (RENT_GROWTH_MULT ** (state.rent_cycle + 1)))

            chosen_upgrade = None
            if strategy == "greedy":
                offers.sort(key=lambda x: x["cost"], reverse=True)
                for off in offers:
                    cost_mult = 1.0
                    if daily_ev == "EventOpportunityDay":
                        cost_mult = ev_mods.get("upgrade_cost_mult", 1.0)
                    actual_cost = int(off["cost"] * cost_mult)

                    if state.money >= actual_cost:
                        if off["id"] == "emergency_brake" or (state.money - actual_cost) >= next_rent:
                            chosen_upgrade = off
                            break
            elif strategy == "sensible":
                remaining_days = MAX_DAYS - state.day
                offers.sort(key=lambda x: x["cost"], reverse=True)
                for off in offers:
                    cost_mult = 1.0
                    if daily_ev == "EventOpportunityDay":
                        cost_mult = ev_mods.get("upgrade_cost_mult", 1.0)
                    actual_cost = int(off["cost"] * cost_mult)

                    if state.money >= actual_cost:
                        if off["id"] == "emergency_brake" or (state.money - actual_cost) >= next_rent:
                            if actual_cost < remaining_days * 50:
                                chosen_upgrade = off
                                break

            if chosen_upgrade:
                actual_cost = int(chosen_upgrade["cost"])
                if daily_ev == "EventOpportunityDay":
                    actual_cost = int(actual_cost * ev_mods.get("upgrade_cost_mult", 1.0))
                state.modify_money(-actual_cost)
                state.upgrades.append(chosen_upgrade["id"])

                if chosen_upgrade["type"] == "reward_mult": reward_mult *= chosen_upgrade["val"]
                if chosen_upgrade["type"] == "speed_mult": speed_mult *= chosen_upgrade["val"]
                if chosen_upgrade["type"] == "emergency": has_emergency_brake = True

        if state.day % RENT_INTERVAL_DAYS == 0:
            if state.money < state.get_rent() and has_emergency_brake:
                has_emergency_brake = False

            state.process_rent()

        if state.game_over:
            break

        if state.day in state.money_at_day:
            state.money_at_day[state.day] = state.money

        state.day += 1

    if not state.game_over and state.day > MAX_DAYS:
        state.win = True

    return state

def main():
    parser = argparse.ArgumentParser(description='Cargor Monte Carlo Economy Simulator')
    parser.add_argument('--test-run', action='store_true', help='Run a small number of iterations for testing')
    parser.add_argument('--what-if', action='store_true', help='Run with what-if scenario (events on every non-rent day)')
    args = parser.parse_args()

    print("Test mode:", args.test_run)
    print("What-if mode:", args.what_if)

    # 1,112 runs * 4 counts * 3 profiles * 3 strategies = 40032 total runs per scenario.
    num_runs_per_config = 10 if args.test_run else 1112

    results = []

    for p_count in [1, 2, 3, 4]:
        for profile in PROFILES.keys():
            for strategy in STRATEGIES:
                for speed_mod in [1.0]: # Default speed
                    wins = 0
                    bankruptcies = 0
                    money_finals = []
                    rent_day_fails = {4:0, 8:0, 12:0, 16:0}

                    d3_money = []
                    d5_money = []
                    d8_money = []
                    d12_money = []
                    win_flags = []

                    for run_idx in range(num_runs_per_config):
                        seed = SEED + run_idx + (p_count * 10000) + (hash(profile) % 10000) + (hash(strategy) % 1000)
                        state = run_simulation(p_count, profile, strategy, seed, args.what_if, speed_mod)

                        if state.win:
                            wins += 1
                        if state.bankruptcy:
                            bankruptcies += 1
                            if state.rent_day_bankrupt in rent_day_fails:
                                rent_day_fails[state.rent_day_bankrupt] += 1

                        money_finals.append(state.money)

                        d3_money.append(state.money_at_day[3])
                        d5_money.append(state.money_at_day[5])
                        d8_money.append(state.money_at_day[8])
                        d12_money.append(state.money_at_day[12])
                        win_flags.append(1 if state.win else 0)

                    win_pct = wins / num_runs_per_config
                    bank_pct = bankruptcies / num_runs_per_config

                    money_finals.sort()
                    med_money = np.median(money_finals)
                    p10_money = np.percentile(money_finals, 10)
                    p90_money = np.percentile(money_finals, 90)

                    corr_d3 = np.corrcoef(d3_money, win_flags)[0, 1] if np.std(d3_money) > 0 and np.std(win_flags) > 0 else 0
                    corr_d5 = np.corrcoef(d5_money, win_flags)[0, 1] if np.std(d5_money) > 0 and np.std(win_flags) > 0 else 0
                    corr_d8 = np.corrcoef(d8_money, win_flags)[0, 1] if np.std(d8_money) > 0 and np.std(win_flags) > 0 else 0
                    corr_d12 = np.corrcoef(d12_money, win_flags)[0, 1] if np.std(d12_money) > 0 and np.std(win_flags) > 0 else 0

                    results.append({
                        "p_count": p_count,
                        "profile": profile,
                        "strategy": strategy,
                        "speed_mod": speed_mod,
                        "what_if": args.what_if,
                        "win_pct": win_pct,
                        "bank_pct": bank_pct,
                        "med_money": med_money,
                        "p10_money": p10_money,
                        "p90_money": p90_money,
                        "fail_d4": rent_day_fails[4],
                        "fail_d8": rent_day_fails[8],
                        "fail_d12": rent_day_fails[12],
                        "fail_d16": rent_day_fails[16],
                        "corr_d3": corr_d3,
                        "corr_d5": corr_d5,
                        "corr_d8": corr_d8,
                        "corr_d12": corr_d12
                    })

    fname = "tools/jules_mc/results/sim_results_whatif.csv" if args.what_if else "tools/jules_mc/results/sim_results.csv"
    with open(fname, "w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=results[0].keys())
        writer.writeheader()
        for r in results:
            writer.writerow(r)

    print(f"Done. Wrote {len(results)} rows to {fname}")

if __name__ == '__main__':
    main()
