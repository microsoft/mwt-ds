# Simulated Data Generator

The Jupyter Notebook <i>Simulated_Data_Generator.ipynb</i> in this folder can generate a synthetic dataset and simulate an experiment to get DSJson logs. 

## Overview
To generate the dataset and logs, prepare a config file (described later) and then run the Notebook <i>Simulated_Data_Generator.ipynb</i> end to end.
There are two parts in the notebook:

1. Generate a Simulated Dataset
    * Use the config file to generate a dataset with the specified context, action and rewards. We refer to this as the ground truth file.


2. Transform to DSJson and Train a VW Model
    * At each iteration, randomly sample a batch from the ground truth file.
    * Get actions according to the latest model predictions.
    * Get rewards from the ground truth file.
    * Send the batch to VW for training and update the model.
    * Save the logs for each batch separately. These logs will be used for the Context Explorer.
    * This whole process simulated an experiment in which VW learns a policy to maximize reward for the ground truth data

## Config File
The key input to the notebook is the config file <i>config_data_generator.json</i>. Here is an example of the config file and some details:

    {
        "dataset_name": "Test",
        "output_folder": "E:\\data\\20190729_context_explorer\\simulated_data",
        "reward_range": [-1, 1],
        "reward_dense_range": [0, 0.3],
        "actions": [1, 2, 3, 4, 5, 6, 7, 8],
        "contexts": {
            "CallType": ["1_1", "GVC"],
            "MediaType": ["Audio", "Video"],
            "NetworkType": ["wifi", "wired"]
        },
        "context_action_size": 1000,
        "increase_winning_margin": 0.02,
        "center": true,
        "p_value": 0.001,
        "random_state": 3,
        "model_parameters": {
            "batch_size_initial": 5000,
            "batch_size":5000,
            "iterations": 30,
            "default_action_index": 0,
            "add_control_group": false
        },
        "vw_commands":{
            "exploration_policy": "--epsilon 0.3",
            "cb_type": "ips",
            "interactions": "--interactions iFFF",
            "learning_rate": 0.001,
            "other_commands": "--power_t 0"
        }
    }

* **dataset_name** [str]: Name of the dataset
* **output_folder** [str]: Path where the dataset will be saved. Note that the DSJson logs will be saved to **output_folder\logs**. 
* **reward_range** [list]: The reward boundaries
* **reward_dense_range** [list]: The reward range where most values should fall into

* **actions** [list]: List of all possible actions
* **contexts** [dict]: A dictionary of contexts and their unique values. For example `"Color": ["red", "blue"]`
* **context_action_size** [int]: Number of samples for each context*action pair
* **p_value** [float]: _(optional)_ p-value threshold for t-test. Default 0.001
* **increase_winning_margin** [float]: _(optional)_ Add this value to the winning action’s rewards to increase the winning margin. The higher the value, the easier the optimization problem. Default 0
* **center** [bool]: _(optional)_ Center data by removing the mean reward. Default True
* **random_state** [int]: _(optional)_ random seed. Default 1
* **model_parameters** [dict]: 
	* **batch_size_initial** [int]: Sample size for the first iteration
	* **batch_size** [int]: Sample size for the following iterations
	* **iterations** [int]:  Number of iterations
	* **default_action_index** [int]: _(optional)_ Index of the default action in the _“actions”_ list. Default 0 (the first action from the list)
	* **add_control_group** [bool]: _(optional)_ To create a proper control group, whose data will not be used to train the policy. Default False
* **vw_commands** [dict]: 
	* **exploration_policy** [str]: _(optional)_ Default "--epsilon 0.3"
	* **cb_type** [str]: _(optional)_ Default "ips"
	* **interactions** [str]: _(optional)_ Default "--interactions iFFF"
	* **learning_rate** [float]: _(optional)_ Default 0.001
	* **other_commands** [str]: _(optional)_ Default ""
