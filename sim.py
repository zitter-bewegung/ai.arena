import requests
import json
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

BASE_URL = "https://localhost:7065" 
ENDPOINT = "/BattleCalculator/calculate-specific-units-team" 
ITERATIONS = 1000  # Кількість битв

# Вибір юнітів для симуляції
UNIT_A_ID = 1
UNIT_B_ID = 4

# Імена юнітів для виводу
UNIT_NAMES = {
    0: "Light",
    1: "Heavy",
    2: "Fast",
    3: "ShortRange",
    4: "LongRange"
}

def run_simulation():
    url = BASE_URL + ENDPOINT
    headers = {"Content-Type": "application/json", "accept": "text/plain"}
    
    name_a = UNIT_NAMES.get(UNIT_A_ID, "Unknown")
    name_b = UNIT_NAMES.get(UNIT_B_ID, "Unknown")

    payload = {
        "battle-id": "sim_auto",
        "unit-type-A": UNIT_A_ID,
        "unit-type-B": UNIT_B_ID
    }

    wins_a = 0
    wins_b = 0
    errors = 0

    print(f"Починаємо симуляцію: {ITERATIONS} битв")
    print(f" {name_a} (ID {UNIT_A_ID})  VS  {name_b} (ID {UNIT_B_ID})")
    print("-" * 40)

    for i in range(ITERATIONS):
        try:
            response = requests.post(url, json=payload, headers=headers, verify=False)
            
            if response.status_code == 200:
                data = response.json()
                winner = data.get("winner", "")
                if name_a in winner:
                     wins_a += 1
                elif name_b in winner:
                     wins_b += 1
                
            else:
                print(f"Помилка сервера: {response.status_code}")
                errors += 1
                
        except Exception as e:
            print(f"Помилка: {e}")
            errors += 1
            
        if (i + 1) % 10 == 0:
            print(".", end="", flush=True)

    print("\n" + "-" * 40)
    print("📊 РЕЗУЛЬТАТИ:")
    if errors > 0: print(f"Помилок: {errors}")
    
    # Розрахунок відсотків
    rate_a = (wins_a / ITERATIONS) * 100 
    rate_b = (wins_b / ITERATIONS) * 100

    print(f"{name_a}: {wins_a} перемог ({rate_a:.1f}%)")
    print(f"{name_b}: {wins_b} перемог ({rate_b:.1f}%)")

if __name__ == "__main__":
    run_simulation()  


