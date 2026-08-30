import cma
import requests
import json
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

def run_simulation(theta):
    # print("Running simulation with parameters:", theta)
    # url = "https://localhost:7065/Balancing/objective-function/specific-team" 
    url = "https://localhost:7065/Balancing/objective-function/winner-contribution" 

    ad = [round(x * 30) for x in theta]
    payload = {
        "Light": {"type": 0, "attack": ad[0], "defence": ad[1], "range": 1, "movement": 12},
        "Heavy": {"type": 1, "attack": ad[2], "defence": ad[3], "range": 1, "movement": 5},
        "Fast": {"type": 2, "attack": ad[4], "defence": ad[5], "range": 1, "movement": 17},
        "ShortRange": {"type": 3, "attack": ad[6], "defence": ad[7], "range": 7, "movement": 5},
        "LongRange": {"type": 4, "attack": ad[8], "defence": ad[9], "range": 17, "movement": 2}
    }

    response = requests.post(url, json=payload, verify=False) 
    loss = float(response.text)
    return loss

def objective(theta):
    return run_simulation(theta)  # your expensive function

initial_parameters = [28, 11, 7, 30, 29, 11, 17, 5, 0, 10]  # example initial guess
normalized_parameters = [x / 30 for x in initial_parameters]  # normalize to [0, 1]

es = cma.CMAEvolutionStrategy(normalized_parameters, 0.5, {
        "bounds": [0, 1],
        "CMA_active": True,
        "maxfevals": 2000
    })  # initial guess + sigma

print("Starting optimization with initial parameters (normalized):", normalized_parameters)
while not es.stop():
    solutions = es.ask()
    losses = [objective(x) for x in solutions]
    es.tell(solutions, losses)
    es.disp()
    print("Current best result:")
    print(es.result_pretty())
    # es.result_pretty()

best = es.result.xbest

print("Best loss:", json.dumps(es.result))
print("Best parameters (normalized):", best)
print("Best parameters (original scale):", [x * 30 for x in best])