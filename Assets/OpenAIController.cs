﻿using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Text;

using System.Net.Http;
using Unity.VisualScripting;
using UnityEngine.UIElements;

// TODO: Finish the fourth iteration of the prompt I am working on
// TODO: Remove the walkable tile from the JSON and document that solution. Make sure to 
// just create a new json file for them and have a way to switch back and forth. Be descriptive.
// TODO: Remove the 'role' variable from the character_data.json and see if less helps the model

public enum Prompt_Selected
{
    PS_Brief_Paragraph = 1,
    PS_Descriptive_Paragraph,
    PS_Brief_List,
    PS_Descriptive_List
}

public class OpenAIController : MonoBehaviour {

    private OpenAIAPI api;
    private List<ChatMessage> messages;

    public const int grid_width = 10;
    public const int grid_height = 10;

    private GameObject[,] instantiated_player_tiles = new GameObject[grid_width, grid_height];

    private Dictionary<string, Character> characters_by_id = new Dictionary<string, Character>();

    // Create a 10x10 character_Grid to hold the IDs (2D array)
    string backstory_global;
    string[,] world_grid_global = new string[grid_width, grid_height];
    string[,] character_Grid = new string[grid_width, grid_height];
    string walkable_block_ids;

    string environment_data_string;

    public int time_of_day = 0;
    public TMP_Text time_display_text;
    const int time_increment = 400;

    // const Prompt_Selected current_prompt = Prompt_Selected.PS_Brief_Paragraph;
    // const Prompt_Selected current_prompt = Prompt_Selected.PS_Descriptive_Paragraph;
    // const Prompt_Selected current_prompt = Prompt_Selected.PS_Brief_List;
    const Prompt_Selected current_prompt = Prompt_Selected.PS_Descriptive_List;

    void Start()
    {
        ClearInstantiatedTiles();
        // 1200 pm starting time
        set_time_of_day(1600);
        // NOTE: Previous OpenAI Key
        string api_key = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(api_key)) {
            Debug.Log("Error: API key is null or empty");
        }
        else {
            api = new OpenAIAPI(new APIAuthentication(api_key));
            Debug.Log("API initialized successfully.");
        }

        InitializeGame().ConfigureAwait(false);
    }
    private void update_time_display()
    {
        // Format the time (e.g., 1300 -> "13:00", 900 -> "9:00")
        string hours = (time_of_day / 100).ToString("D2"); // Get hours (e.g., 13 from 1300)
        string minutes = (time_of_day % 100).ToString("D2"); // Get minutes (e.g., 00 from 1300)
        string formattedTime = $"{hours}:{minutes}";

        // Set the text on the TMP_Text component
        time_display_text.text = formattedTime;
    }
    private void set_time_of_day(int newTime)
    {
        // normalise to 0-2399 first (handles negatives too)
        newTime = ((newTime % 2400) + 2400) % 2400;

        int hours   =  newTime / 100;
        int minutes =  newTime % 100;

        hours   += minutes / 60;
        minutes  = minutes % 60;

        time_of_day = (hours % 24) * 100 + minutes;
        Debug.Log($"[{System.Threading.Thread.CurrentThread.ManagedThreadId}] clock = {time_of_day:D4}");
        update_time_display();
    }
    // Method to format and display the time on the UI
    private void increment_world_clock()
    {
        int previous_time_of_day = time_of_day;
        set_time_of_day(previous_time_of_day + time_increment);
    }

    private async Task InitializeGame()
    {
        // Find the JSON file (Application.dataPath points to the location where the game's data is stored)
        // Creates a file path to the JSON. Fore example: asset\background_Settings.json
        string environment_data_file_path = Path.Combine(Application.dataPath, "environment_data.json");
        await GenerateWorldWithChatGPT(environment_data_file_path); 

        // NOTE: Only start after the GenerateWorldWithChatGPT as finished!
        string character_data_initial_file_path = Path.Combine(Application.dataPath, "character_data_initial.json");
        if (character_data_initial_file_path == null) {
            Debug.Log("ERROR: Filepath is null");
        }
        await PlaceCharactersInWorldAndUpdate(character_data_initial_file_path); 
    }
    public class EnvironmentData 
    {
        public string BackgroundStory { get; set; }
        public List<EnvironmentTile> EnvironmentTiles { get; set; }
    }
    public class CharacterData {
        public List<Character> Characters { get; set; }
    }
    public class EnvironmentTile
    {
        public string ObjectID { get; set; }
        public string TileType { get; set; }
        public bool Walkable { get; set; }
    }

    public class Character
    {
        public string ObjectID { get; set; }
        public string Type { get; set; }
        public string CharacterModelName { get; set; }
        public string Role { get; set; }
        public List<string> DayTasks { get; set; }
        public List<string> NightTasks { get; set; }
    }
    private EnvironmentData LoadEnvironmentDataFromJson(string file_path)
    {
        // Read the text from the specified file
        string jsonContent = File.ReadAllText(file_path);
        
        // Convert the text into a EnvironmentData object
        var environment_data = JsonConvert.DeserializeObject<EnvironmentData>(jsonContent);

        // Construct a string representing walkable blocks
        walkable_block_ids = GetWalkableBlocksString(environment_data);

        return environment_data;
    }

    // Helper function to construct the walkable blocks string
    private string GetWalkableBlocksString(EnvironmentData environment_data)
    {
        if (environment_data == null)
        {
            Debug.LogError("Environment data is null in GetWalkableBlocksString.");
            return string.Empty;
        }

        if (environment_data.EnvironmentTiles == null)
        {
            Debug.LogError("EnvironmentTiles is null in GetWalkableBlocksString.");
            return string.Empty;
        }

        // Create a string to store walkable block IDs
        List<string> result = new List<string>();

        // Iterate through environment tiles and add walkable tiles to the list
        foreach (var tile in environment_data.EnvironmentTiles)
        {
            if (tile != null && tile.Walkable) 
            {
                result.Add(tile.ObjectID);
            }
        }

        // Return a comma-separated string of walkable block IDs (e.g., "001,003,005")
        return string.Join(",", result);

    }

    private CharacterData LoadCharacterDataFromJson(string file_path)
    {
        // Read the text from the specified file
        string jsonContent = File.ReadAllText(file_path);

        // Convert the text into a CharacterData object
        var characterData = JsonConvert.DeserializeObject<CharacterData>(jsonContent);

        // Store the characters in a dictionary for easy lookup
        foreach (var character in characterData.Characters)
        {
            characters_by_id[character.ObjectID] = character;
        }

        return characterData;
    }

    // NOTE: FIRST GENERATION PROMPT
    private async Task GenerateWorldWithChatGPT(string file_path)
    {
        // Debug.Log("Inside GenerateWorldWithChatGPT");
        if (File.Exists(file_path))
        {
            EnvironmentData environment_data = LoadEnvironmentDataFromJson(file_path);
            backstory_global = JsonConvert.SerializeObject(environment_data.BackgroundStory, Formatting.None);
            Debug.Log("Back Story Global\n" + backstory_global);
            // NOTE: Convert the EnvironmentData type back into a json string
            environment_data_string = JsonConvert.SerializeObject(environment_data, Formatting.None);

            // For this prompt response, we only want it to generate the terrain (ignoring the character ids)

            string prompt = "";
            if (current_prompt == Prompt_Selected.PS_Brief_Paragraph) {
                prompt =
                    "Construct a 10x10 grid of ObjectIDs that represents a 2D game world. " +
                    "The ObjectIDs are provided in the environment_data.json file in the " +
                    "EnvironmentTiles section. Each ObjectID corresponds to a different " +
                    "TileType. The game world you construct should be constructed based on " +
                    "the BackgroundStory description that is provided in the " +
                    "environment_data.json file provided below. Format the grid as a table " +
                    "with 10 rows and 10 columns, where each cell contains three-digit " +
                    "ObjectID of the TileType which are provided in the environment_data.json " +
                    "file. Separate each ID with a pipe | symbol and terminate each row with " +
                    "a newline character \\n. Here is an example row: " +
                    "001|001|001|001|001|001|001|001|001|001\\n\n" +
                    "Here is the environment_data.json\n\n" +

                    environment_data_string + "\n\n" +

                    "Respond only with the 10x10 grid.";

            }
            else if (current_prompt == Prompt_Selected.PS_Descriptive_Paragraph)
            {
                prompt = 
                    "Construct a 10x10 grid of ObjectIDs that represents a 2D game world. " +
                    "Make note of the environment_data.json file that I have provided later " +
                    "in this prompt. The ObjectIDs are provided in this environment_data.json " +
                    "file, in the EnvironmentTiles section. Each ObjectID corresponds to a " +
                    "different TileType. The TileTypes represent the world blocks like grass, " +
                    "rock, etc. The BackgroundStory section of the environment_data.json file " +
                    "provides a description of the game world. The 10x10 grid of the 2D game " +
                    "world you construct should be constructed based on the BackgroundStory " +
                    "description that is provided in the environment_data.json file provided " +
                    "below. Format the grid as a table with 10 rows and 10 columns, where each " +
                    "cell contains three-digit ObjectID of the TileType which are provided in " +
                    "the environment_data.json file. Separate each ID with a pipe | symbol " +
                    "and terminate each row with a newline character \\n. Here is an example " +
                    "row: 001|001|001|001|001|001|001|001|001|001\\n\n" + 
                    "Here is the environment_data.json\n\n" +

                    environment_data_string + "\n\n" +

                    "Respond only with the 10x10 grid.";            
            }
            else if (current_prompt == Prompt_Selected.PS_Brief_List)
            {
                prompt =
                    "Instructions:\n" + 
                    "1. The BackgroundStory section in the environment_data.json file provided " +
                    "below contains a description of what the game world should look like.\n" + 
                    "2. Using the BackgroundStory, construct a 10x10 grid of ObjectIDs that " +
                    "represents a 2D game world. The ObjectIDs are provided in the " +
                    "environment_data.json file in the EnvironmentTiles section. Each ObjectID " +
                    "corresponds to a different TileType.\n" + 
                    "Grid Format:\n" + 
                    "1. The grid should have 10 rows and 10 columns, where each cell contains " +
                    "the three-digit ObjectId of the corresponding TileType. Separate each " +
                    "ObjectID with a pipe | symbol and terminate each row with a newline " +
                    "character \\n. Here is an example row: " +
                    "001|001|001|001|001|001|001|001|001|001|\\n\n" +
                    "2. Only respond with the 10x10 grid." + 
                    "Here is the environment_data.json\n\n" +

                    environment_data_string + "\n\n" +

                    "Respond only with the 10x10 grid.";     
            }
            else if (current_prompt == Prompt_Selected.PS_Descriptive_List)
            {
                // Duplicate from PS_Brief_List because it is very effective so far
                prompt =
                    "Instructions:\n" + 
                    "1. The BackgroundStory section in the environment_data.json file provided " +
                    "later in this prompt contains two sections titled BackgroundStory and " +
                    "EnvironmentTiles. The BackgroundStory section contains a description of " +
                    "what the 10x10 grid of the 2D game world should look like. The " +
                    "EnvironmentTiles sections contain the TileTypes with their corresponding " +
                    "ObjectIDs and other relevant variables.\n" + 
                    "2. The grid needs to visually represent the BackgroundStory. Using the " +
                    "BackgroundStory, construct a 10x10 grid of ObjectIDs that represents a " +
                    "2D game world. The ObjectIDs are provided in the environment_data.json " +
                    "file in the EnvironmentTiles section of the json file provided later in " +
                    "this prompt. Each ObjectID corresponds to a different TileType. The " +
                    "TileTypes represent the world blocks like grass, rock, etc.\n" +
                    "Grid Format:\n" +
                    "1. The grid should have 10 rows and 10 columns, where each cell contains " +
                    "the three-digit ObjectId of the corresponding TileType. Separate each " +
                    "ObjectID with a pipe ‘|’ symbol and terminate each row with a newline " +
                    "character ‘\\n’. Here is an example row: " +
                    "001|001|001|001|001|001|001|001|001|001|\\n\n" +
                    "2. It is very important that you only respond with the 10x10 grid and no " +
                    "additional text or artifact.\n" +
                    "Here is the environment_data.json\n\n" +

                    environment_data_string + "\n\n" +

                    "Respond only with the 10x10 grid.";   
            }
            else
            {
                Debug.Log("ERROR: Prompt not initialized.");
            }


            if (prompt == "")
            {
                Debug.Log("ERROR: Prompt did not initialize properly.");
            }

            Debug.Log("Prompt 1 - World Generation: \n" + prompt);

            // Debug.Log($"Walkable Block IDs: {walkable_block_ids}");

            ChatResult chat_gpt_result = await SendPromptToChatGPT(prompt);

            // NOTE: Process the response from ChatGPT
            if (chat_gpt_result != null && chat_gpt_result.Choices != null && chat_gpt_result.Choices.Count > 0)
            {
                // NOTE: Pull the text from the ChatResult struct
                string chat_gpt_string = chat_gpt_result.Choices[0].Message.TextContent;
                Debug.Log("Response 1 - World Generation: \n" + chat_gpt_string);
                InstantiateWorldGrid(chat_gpt_string);
                PrintGridToDebug("Instantiation 1: World Grid", world_grid_global);
            }
            else
            {
                Debug.LogError("ChatResult is null or does not contain choices.");
            }
        }
        else
        {
            Debug.LogError("Cannot find the JSON file: " + file_path + "\n\n");
        }
    }

    // NOTE: SECOND GENERATION PROMPT
    private async Task PlaceCharactersInWorldAndUpdate(string character_data_json)
    {
        // Debug.Log("Inside PlaceCharactersInWorldCoroutine");
        CharacterData character_data_json_loaded = LoadCharacterDataFromJson(character_data_json);
        string character_data_string = JsonConvert.SerializeObject(character_data_json_loaded , Formatting.None);
        string world_Grid_String = GridToString(world_grid_global);

        string formatted_time = time_of_day.ToString("D4");
        increment_world_clock();

        string prompt = "";
        if (current_prompt == Prompt_Selected.PS_Brief_Paragraph)
        {
            prompt =
                "Using four inputs - environment_data.json (BackgroundStory plus each " +
                "tile's ObjectID, TileType, and walkable flag), the 10 x 10 " +
                "current_world_grid, character_data.json (CharacterID, Role, " +
                "DayTasks, NightTasks, and each character's ObjectID), and " +
                "non_walkable_tiles - place every character on one valid tile " +
                "(walkable == true and ObjectID not in non_walkable_tiles), " +
                "preferring a tile whose TileType fits the Role when possible. " +
                "Replace that tile with CharacterID,Task, selecting Task from " +
                "DayTasks when the current time, which is " + formatted_time + " is " +
                "between 0600 and 1759, otherwise from NightTasks. Leave all other tiles " +
                "unchanged. Return only the updated grid: 10 rows separated by " +
                "newlines, separate cells with | and end each row with \\n\n" +

                "For clarity, a nighttime grid row might look like this: " +
                "001|001|001|101,resting|001|001|001|001|001|001|\\n\n" +

                "The current world time is - " + formatted_time + ".\n" +

                "Here is the environment_data.json file:\n" +
                environment_data_string + "\n\n" +

                "Here is the current_world_grid:\n" +
                world_Grid_String + "\n\n" +

                "Here is the character_data.json file:\n" +
                character_data_string + "\n\n" +

                "Here are the non walkable tiles:\n" +
                GetNonWalkableTiles(environment_data_string) + "\n\n" +

                "Only respond with the updated 10x10 grid with the characters placed " +
                "on it and their new tasks. No additional artifacts.";
        }
        else if (current_prompt == Prompt_Selected.PS_Descriptive_Paragraph)
        {
             prompt =
                "Below you will find four essential pieces of reference material. First " +
                "comes the environment_data.json file, whose BackgroundStory section lays " +
                "out the narrative setting while its EnvironmentTiles section lists every " +
                "TileType together with the ObjectID that has already been used to assemble " +
                "the 10×10 current_world_grid. Second is the current_world_grid itself—the " +
                "live map of the world that you are about to edit. Third is the " +
                "character_data.json file; each entry there supplies a Character’s " +
                "ObjectID, Role, and separate DayTasks and NightTasks lists. Fourth is an " +
                "explicit list of tile ObjectIDs that are not walkable. Your job is to " +
                "take every Character defined in character_data.json (if only two are " +
                "present, place only those two) and insert them into suitable positions " +
                "on the current_world_grid by overwriting the target tile’s ObjectID with " +
                "the Character’s ObjectID. A tile is “suitable” only if its walkable flag " +
                "in environment_data.json is set to true and its ObjectID does not appear " +
                "in the non-walkable list; in addition, prefer a location that makes " +
                "narrative sense for the Character’s Role. After placing each Character, " +
                "assign that Character a current task in the format CharacterID,Task—for " +
                "example, 101,resting. Choose the task from the Character’s DayTasks " +
                "section when the current time (which is " + formatted_time + ") falls between " +
                "0600 and 1759, otherwise choose from NightTasks. " +

                "For clarity, a nighttime grid row might look like this: " +
                "001|001|001|101,resting|001|001|001|001|001|001|\\n\n" +

                "Here is the environment_data.json file:\n" +
                environment_data_string + "\n\n" +

                "Here is the current_world_grid:\n" +
                world_Grid_String + "\n\n" +

                "Here is the character_data.json file:\n" +
                character_data_string + "\n\n" +

                "Here are the non walkable tiles:\n" +
                GetNonWalkableTiles(environment_data_string) + "\n\n" +

                "Respond only with the updated 10×10 grid, showing the Characters in " + 
                "place and their new tasks, and do not include any additional commentary " +
                "or artifacts.";
        } else if (current_prompt == Prompt_Selected.PS_Brief_List) { 
            prompt = 
                "1. Inputs\n" +
                "- environment_data.json – tile types, ObjectIDs, walkable flag\n" +
                "- character_data.json – CharacterID, Role, DayTasks, NightTasks\n" +
                "- current_world_grid – 10 × 10 map of tile ObjectIDs\n" +
                "2. Place characters\n" +
                "- For each character present (place only those listed) choose one tile whose " +
                "walkable flag is true and whose ObjectID is not in the non-walkable list.\n" +
                "- Pick a location that suits the character’s Role.\n" +
                "- Overwrite that tile’s ObjectID with the character’s ObjectID.\n" +
                "3. Assign tasks\n" +
                "- Format: CharacterID,Task (e.g., 101,resting).\n" +
                "- Use a DayTask if the time " + formatted_time + " is 0600–1759; otherwise use a NightTask.\n" +
                "4. Output grid\n" +
                "- 10 rows × 10 columns.\n" +
                "- Each cell holds a three-digit ID or CharacterID,Task.\n" +
                "- Separate cells with | and end each row with \\n\n" +
                "- Respond only with the grid—no extra text.\n" +

                "For clarity, a nighttime grid row might look like this: " +
                "001|001|001|101,resting|001|001|001|001|001|001|\\n\n" +

                "Here is the environment_data.json file:\n" +
                environment_data_string + "\n\n" +

                "Here is the current_world_grid:\n" +
                world_Grid_String + "\n\n" +

                "Here is the character_data.json file:\n" +
                character_data_string + "\n\n" +

                "Here are the non walkable tiles:\n" +
                GetNonWalkableTiles(environment_data_string) + "\n\n" +

                "Respond only with the updated 10×10 grid, showing the Characters in " + 
                "place and their new tasks, and do not include any additional commentary " +
                "or artifacts.";

        } else if (current_prompt == Prompt_Selected.PS_Descriptive_List) {
            prompt =
                "1. Reference files provided\n" +
                "- environment_data.json – contains two key subsections:\n" +
                "-- BackgroundStory – a prose overview of the world’s lore, setting, and ambience.\n" +
                "-- EnvironmentTiles – a catalogue of every TileType paired with its three-digit ObjectID; these IDs " +
                "already populate the 10 × 10 current_world_grid.\n" +
                "- current_world_grid – the live 10 × 10 map whose cells you will modify.\n" +
                "- character_data.json – lists each Character’s ObjectID, Role, plus separate DayTasks and NightTasks arrays.\n" +
                "2. Non-walkable tiles list – a set of tile ObjectIDs on which no character may stand.\n" +
                "3. Character placement rules\n" +
                "- For every character appearing in character_data.json (if only two are listed, place exactly two), locate " +
                "a tile in the grid whose walkable flag is true and whose ObjectID is absent from the non-walkable list.\n" +
                "- Choose a position that thematically matches the character’s Role (e.g., a Fisher by water, a Guard near a gate).\n" +
                "- Overwrite that tile’s ObjectID with the character’s ObjectID, thereby embedding the character on the map.\n" +
                "4. Task assignment logic\n" +
                "- After placement, assign each character a current task in the form CharacterID,Task (example: 101,resting).\n" +
                "- Use the current time " + formatted_time + " (military format) to decide which task set to draw from:\n" +
                "- Daytime (0600 – 1759): select from DayTasks.\n" +
                "- Nighttime (1800 – 0559): select from NightTasks.\n" +
                "5. Grid output format\n" +
                "- Maintain exactly 10 rows × 10 columns.\n" +
                "- In each cell, present either a three-digit tile ObjectID or a CharacterID,Task pair.\n" +
                "- Separate all cells with the pipe symbol |, and terminate every row with a newline \\n.\n" +
                "- Example nighttime row: 001|001|001|101,resting|001|001|001|001|001|001|\\n.\n" +
                "6. Response requirements\n" +
                "- Return only the fully updated 10 × 10 grid—no headings, explanations, or extra text.\n" +
                "- Ensure that every character stands on a walkable tile and that no non-walkable tile is replaced.\n" +

                "Here is the environment_data.json file:\n" +
                environment_data_string + "\n\n" +

                "Here is the current_world_grid:\n" +
                world_Grid_String + "\n\n" +

                "Here is the character_data.json file:\n" +
                character_data_string + "\n\n" +

                "Here are the non walkable tiles:\n" +
                GetNonWalkableTiles(environment_data_string) + "\n\n" +

                "Respond only with the updated 10×10 grid, showing the Characters in " + 
                "place and their new tasks, and do not include any additional commentary " +
                "or artifacts.";
        
        } else
        {
            Debug.Log("ERROR: Prompt not initialized.");
        }


        // TODO: Add more debug code here

        Debug.Log("Prompt 2: Character Placement\n" + prompt);

        // Send the prompt to ChatGPT
        ChatResult chat_gpt_result = await SendPromptToChatGPT(prompt);

        // NOTE: Process the response from ChatGPT
        if (chat_gpt_result != null && chat_gpt_result.Choices != null && chat_gpt_result.Choices.Count > 0)
            {
            var chat_gpt_string =  chat_gpt_result.Choices[0].Message.TextContent;
            Debug.Log("Response 2: Character Placement\n" + chat_gpt_string);
            InstantiateCharacterGrid(chat_gpt_string);
            PrintGridToDebug("Instantiation 2: Character Placement", character_Grid);

            // After processing initial placements, I can instantiate the grid to reflect these placements
            // PrintGridToDebug("1st Character Grid Instantiation", character_Grid);
            // InstantiateGrid(character_Grid, 1);

            // Start a routine after the initial placement of the characters
            StartCoroutine(UpdateCharacterPositionsCoroutine(character_data_json));
        }
    }

    // NOTE: THIRD GENERATION PROMPT (RECURRING)
    private IEnumerator UpdateCharacterPositionsCoroutine(string file_path_initial_placement)
    {
        // Debug.Log("updating_format_specification_string:\n" + updating_format_specification_string);

        CharacterData initial_placement = LoadCharacterDataFromJson(file_path_initial_placement);
        string character_data_string = JsonConvert.SerializeObject(initial_placement, Formatting.None);

        string character_Grid_String = GridToString(character_Grid);
        string world_Grid_String = GridToString(world_grid_global);
        string formatted_time = time_of_day.ToString("D4");

        // Debug.Log("Character_IDs\n" + character_IDs);

        // Start count at 3 because of two prompts prior
        int count = 3;
        // GAMELOOP
        while (true)
        {

            increment_world_clock();
            formatted_time = time_of_day.ToString("D4");
            string prompt = "";
            if (current_prompt == Prompt_Selected.PS_Brief_Paragraph)
            {
                prompt =
                    "Using four inputs – environment_data.json (BackgroundStory plus each tile's " +
                    "ObjectID, TileType, and walkable flag), current_world_grid (10 x 10 grid of " +
                    "ObjectIDs), current_character_grid (each character's current position), and " +
                    "character_data.json (CharacterID, ObjectID, Role, DayTasks, NightTasks) – " +
                    "move every character exactly one tile north, south, east, or west onto a " +
                    "destination tile where walkable == true and the ObjectID is from " +
                    "environment_data.json. Pick the direction that best fits the character's " +
                    "current Task and the BackgroundStory. Replace the destination tile's " +
                    "ObjectID with CharacterID,Task, choosing Task from DayTasks when HHMM is " +
                    "0600-1759, otherwise from NightTasks, and pick the task that makes the most " +
                    "narrative sense. Leave all other cells unchanged. Respond only with the updated 10 × 10 grid, " +
                    "using three-digit EnvironmentTile IDs or CharacterID,Task pairs, separating " +
                    "cells with | and ending each row with a newline (\\n)." +

                    "For clarity, a nighttime grid row might look like this: " +
                    "001|001|001|101,resting|001|001|001|001|001|001|\\n\n" +
                    
                    "The current world time is - " + formatted_time + ".\n" +

                    "Here is the environment_data.json file:\n" +
                    environment_data_string + "\n\n" +

                    "Here is the character_data.json file:\n" +
                    character_data_string + "\n\n" +

                    "Here is the current_world_grid:\n" +
                    world_Grid_String + "\n\n" +

                    "Here is the current_character_grid:\n" +
                    character_Grid_String + "\n\n" +

                    "Here are the non walkable tiles:\n" +
                    GetNonWalkableTiles(environment_data_string) + "\n\n" +

                    "Only respond with the updated 10x10 grid with the characters placed " +
                    "on it and their new tasks. No additional artifacts.";
            }
            else if (current_prompt == Prompt_Selected.PS_Descriptive_Paragraph)
            {
                prompt =
                    "Below are all the assets you need: environment_data.json (with BackgroundStory, " +
                    "EnvironmentTiles, and each tile’s walkable flag), character_data.json (listing " +
                    "every CharacterID, Role, and separate DayTasks and NightTasks arrays), the " +
                    "10 × 10 current_world_grid of EnvironmentTile ObjectIDs, and the aligned " +
                    "current_character_grid showing where the characters currently stand. If the " +
                    "current time " + formatted_time + " is daytime (0600 – 1759), move each " +
                    "character exactly one adjacent tile north, south, east, or west onto a square " +
                    "whose walkable flag is true; if no walkable neighbour exists, keep the " +
                    "character in place. If the time is nighttime (1800 – 0559), do not move any " +
                    "characters—leave every character where they are. In both cases, never place " +
                    "(or leave) a character on a tile marked walkable = false. After any required " +
                    "movement, assign each character a task in the format CharacterID,Task—for " +
                    "example, 101,farming—choosing from DayTasks during daytime or from NightTasks " +
                    "at night; where multiple tasks qualify, pick the one that best suits the " +
                    "character’s current situation. Respond only with the updated 10 × 10 grid, " +
                    "using three-digit EnvironmentTile IDs or CharacterID,Task pairs, separating " +
                    "cells with | and ending each row with a newline (\\n). For reference, a " +
                    "valid row looks like 001|001|001|101,farming|001|001|001|001|001|001|\\n\n" +
                                        
                    "The current world time is - " + formatted_time + ".\n" +

                    "Here is the environment_data.json file:\n" +
                    environment_data_string + "\n\n" +

                    "Here is the character_data.json file:\n" +
                    character_data_string + "\n\n" +

                    "Here is the current_world_grid:\n" +
                    world_Grid_String + "\n\n" +

                    "Here is the current_character_grid:\n" +
                    character_Grid_String + "\n\n" +

                    "Here are the non walkable tiles:\n" +
                    GetNonWalkableTiles(environment_data_string) + "\n\n" +

                    "Only respond with the updated 10x10 grid with the characters placed " +
                    "on it and their new tasks. No additional artifacts.";
            }
            else if (current_prompt == Prompt_Selected.PS_Brief_List)
            {
                prompt =
                    "Inputs\n" +
                    "- environment_data.json – tile types, walkable flags, background story\n" +
                    "- current_world_grid – 10 × 10 map already showing characters\n" +
                    "- character_data.json – CharacterID, Role, DayTasks, NightTasks\n" +
                    "- Non-walkable tile list – ObjectIDs that cannot hold characters\n" +
                    "Movement rules\n" +
                    "- Daytime (0600 – 1759): move each character one adjacent tile (N, S, E, W) " +
                    "to a square with walkable = true, not in the non-walkable list, and thematically " +
                    "suitable for the Role.\n" +
                    "- Nighttime (1800 – 0559): do not move any characters.\n" +
                    "- If no valid adjacent tile exists, keep the character in place.\n" +
                    "Task assignment\n" +
                    "- Format: CharacterID,Task (e.g., 101,fishing).\n" +
                    "- Daytime -> choose from the character’s DayTasks.\n" +
                    "- Nighttime -> set task to resting (or another NightTask marked “resting” if provided).\n" +
                    "Grid output\n" +
                    "- Return only the 10 rows × 10 columns.\n" +
                    "- Each cell is either a three-digit tile ObjectID or CharacterID,Task.\n" +
                    "- Separate cells with | and end each row with \\n.\n" +
                    "- Respond only with the updated grid—no extra text.\n" +
                             
                    "For clarity, a nighttime grid row might look like this: " +
                    "001|001|001|101,resting|001|001|001|001|001|001|\\n\n" +

                    "The current world time is - " + formatted_time + ".\n" +

                    "Here is the environment_data.json file:\n" +
                    environment_data_string + "\n\n" +

                    "Here is the character_data.json file:\n" +
                    character_data_string + "\n\n" +

                    "Here is the current_world_grid:\n" +
                    world_Grid_String + "\n\n" +

                    "Here is the current_character_grid:\n" +
                    character_Grid_String + "\n\n" +

                    "Here are the non walkable tiles:\n" +
                    GetNonWalkableTiles(environment_data_string) + "\n\n" +

                    "Only respond with the updated 10x10 grid with the characters placed " +
                    "on it and their new tasks. No additional artifacts.";
            }
            else if (current_prompt == Prompt_Selected.PS_Descriptive_List)
            {
                prompt =
                    "Reference materials provided\n" + 
                    "environment_data.json\n" +
                    "• BackgroundStory – narrative context that can influence where a character might logically head next.\n" +
                    "• EnvironmentTiles – every TileType paired with its three-digit ObjectID and a walkable flag indicating whether a character may occupy that tile.\n" +
                    "current_world_grid – the live 10 × 10 map whose cells currently hold either EnvironmentTile ObjectIDs or CharacterID,Task pairs.\n" +
                    "character_data.json – for each character: ObjectID, Role, and separate DayTasks and NightTasks.\n" +
                    "Non-walkable tile list – any tile ObjectID appearing here is permanently off-limits to characters.\n" +
                    "Movement rules\n" +
                    "Daytime (0600 – 1759): move every character exactly one adjacent tile north, south, east, or west. The chosen tile must:\n" +
                    "• have walkable = true in EnvironmentTiles,\n" +
                    "• not appear in the non-walkable list, and\n" +
                    "• make narrative sense given the character’s Role and the BackgroundStory.\n" +
                    "If no eligible neighbour exists, leave the character in place.\n" +
                    "Nighttime (1800 – 0559): characters do not move; they remain on their current tiles.\n" +
                    "Placement mechanics\n" +
                    "When a character moves, overwrite the destination tile’s ObjectID with the character’s ObjectID.\n" +
                    "Never overwrite a tile whose walkable flag is false or whose ID is listed as non-walkable.\n" +
                    "Task assignment\n" +
                    "Represent each task as CharacterID,Task (e.g., 101,farming).\n" +
                    "Daytime → pick a task from the character’s DayTasks that fits the new tile and situation.\n" +
                    "Nighttime → set the task to resting (or the closest NightTask labelled “resting”).\n" +
                    "Grid output requirements\n" +
                    "Return only the updated 10 × 10 grid.\n" +
                    "Each cell contains either a three-digit tile ObjectID or a CharacterID,Task pair.\n" +
                    "Separate cells with a pipe (|) and end every row with a newline (\\n).\n" +
                    "Example nighttime row: 001|001|001|101,resting|001|001|001|001|001|001|\\n.\n" +

                    "The current world time is - " + formatted_time + ".\n" +

                    "Here is the environment_data.json file:\n" +
                    environment_data_string + "\n\n" +

                    "Here is the character_data.json file:\n" +
                    character_data_string + "\n\n" +

                    "Here is the current_world_grid:\n" +
                    world_Grid_String + "\n\n" +

                    "Here is the current_character_grid:\n" +
                    character_Grid_String + "\n\n" +

                    "Here are the non walkable tiles:\n" +
                    GetNonWalkableTiles(environment_data_string) + "\n\n" +

                    "Only respond with the updated 10x10 grid with the characters placed " +
                    "on it and their new tasks. No additional artifacts.";
            }
            else
            {
                Debug.Log("ERROR: Prompt not initialized.");
            }


            Debug.Log("Prompt " + count + ": Updating Characters\n" + prompt);

            // Send the prompt to ChatGPT
            Task<ChatResult> chat_gpt_response = SendPromptToChatGPT(prompt);

            // PrintGridToDebug(character_Grid);
            // Loop through and erase the current character world grid
            for (int i = 0; i < character_Grid.GetLength(0); i++)
            {
                for (int j = 0; j < character_Grid.GetLength(1); j++)
                {
                    character_Grid[i, j] = "";
                }
            }
            // PrintGridToDebug(character_Grid);

            // Wait until the task is completed (Lambda)
            yield return new WaitUntil(() => chat_gpt_response.IsCompleted);

            // Process the response
            if (chat_gpt_response.Status == TaskStatus.RanToCompletion)
            {
                var chat_gpt_result = chat_gpt_response.Result;
                Debug.Log("Response " + count + ": Character Placement\n" + chat_gpt_result);

                InstantiateCharacterGrid(chat_gpt_result.Choices[0].Message.TextContent);
                PrintGridToDebug("Instantiation " + count + ": Character Grid Initial", character_Grid);
            }

            count++;

            // InstantiateGrid(character_Grid, 1);
            // Wait for a specified period before updating again
            yield return new WaitForSeconds(5f);
        }
    }
    private string GetNonWalkableTiles(string environmentData)
    {
        var nonWalkableTiles = new List<string>();

        // Deserialize JSON
        var gameData = JsonConvert.DeserializeObject<EnvironmentData>(environmentData);
        
        foreach (var tile in gameData.EnvironmentTiles)
        {
            // Assuming EnvironmentTile has a Walkable property
            if (!tile.Walkable)
            {
                nonWalkableTiles.Add(tile.ObjectID);
            }
        }

        return string.Join(", ", nonWalkableTiles);
    }

    void InstantiateWorldGrid(string chat_gpt_response)
    {
        // Debug.Log("Inside InstantiateWorldGrid");

        // NOTE: This processes the response from chat GPT to account for 
        // the error responses it has been giving.
        // NOTE: THIS HAS TO BE MORE DYNAMIC
        const string unwanted_phrase = "Here is the 10x10 grid representing a grass field:";
        bool contains_unwanted_phrase = chat_gpt_response.Contains(unwanted_phrase);
        bool contains_unwanted_triple_quotes = chat_gpt_response.Contains("'''");
        if (contains_unwanted_phrase || contains_unwanted_triple_quotes)
        {
            if (contains_unwanted_phrase)
            {
                Debug.Log("Trimming unwanted phrase found in chatGPT's response.");
                int phrase_index = chat_gpt_response.IndexOf(unwanted_phrase) + unwanted_phrase.Length;
                chat_gpt_response = chat_gpt_response.Substring(phrase_index).Trim();
            }

            if (contains_unwanted_triple_quotes)
            {
                Debug.Log("Trimming triple quotes found in chatGPT's repose.");
                // Example: Replace triple quotes with empty space or another string
                chat_gpt_response = chat_gpt_response.Replace("'''", "").Trim();
            }
            /*
            // Split and process the remaining text
            string[] lines_Temp = chat_gpt_response.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Debug.Log("Processed Response:");
            foreach (string line in lines_Temp) {
                Debug.Log(line);
            }
            */
        }
        // Remove the colon that keeps showing up (Bug)
        if (chat_gpt_response.EndsWith(":"))
        {
            chat_gpt_response = chat_gpt_response.Substring(0, chat_gpt_response.Length - 1);
        }

        string[] lines = chat_gpt_response.Trim().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Getting rid of the \n at the end of the lines
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].EndsWith("\\n"))
            {
                int line_new_length = lines[i].Length - 2;
                lines[i] = lines[i].Substring(0, line_new_length).Trim();
            }
        }

        // Debug.Log("***************InstantiateWorldGrid: GPT Response***************");
        // Debug.Log(chat_gpt_response);
        // Debug.Log("******************************************************");
        string[,] grid = new string[grid_width, grid_height];
        // Check that we have 10 lines to match our expected character_Grid size
        if (lines.Length == grid_width)
        {
            // Parse each line
            for (int i = 0; i < lines.Length; i++)
            {
                // Split the line into cells by the pipe character
                string[] cells = lines[i].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                // Check that we have 10 cells in each line
                if (cells.Length == grid_width)
                {
                    for (int j = 0; j < cells.Length; j++)
                    {
                        // Trim the cell to get the ID and assign it to the character_Grid
                        string cell_id = cells[j].Trim();
                        grid[i, j] = cell_id;
                    }
                }
                else
                {
                    Debug.LogError("ERROR: Unexpected number of cells in line: " + lines[i]);
                    // PrintGridToDebug(grid);
                    // Debug.LogError("**************************************************************");
                    // Exit if format is incorrect
                    return;
                }
            }

            // PrintGridToDebug("Instantiation 1", grid);
            // Now I have a character_Grid with IDs, you can instantiate game objects or tiles based on these IDs

            world_grid_global = grid;
            InstantiateGrid(grid, 0);
        }
        else
        {
            // This is for a bug I am getting sometimes. Every now and then, the length returns 11. 
            // Want to print out why when it happens
            Debug.Log("***ERROR: lines.Length == " + lines.Length + "***\n\n");
            for (int i = 0; i < lines.Length; i++)
            {
                Debug.Log(lines[i] + "\n");
            }
        }
    }

    void InstantiateCharacterGrid(string responseText)
    {
        // Debug.Log("Inside InstantiateCharacterGrid\n");
        // Debug.Log(responseText);

        // ERROR CHECKING FOR THE INCORRECT RESPONSES FROM CHATGPT
        const string unwanted_phrase = "Here is the 10x10 grid map represented in text format:";
        bool contains_unwanted_phrase = responseText.Contains(unwanted_phrase);

        bool contains_unwanted_triple_quotes = responseText.Contains("'''");

        // Perform conditional logic based on these checks
        if (contains_unwanted_phrase || contains_unwanted_triple_quotes)
        {
            // If the grid phrase is present, remove it
            if (contains_unwanted_phrase)
            {
                int phrase_index = responseText.IndexOf(unwanted_phrase) + unwanted_phrase.Length;
                responseText = responseText.Substring(phrase_index).Trim();
            }

            // Optionally handIEnumerator le the triple quotes in a specific manner
            if (contains_unwanted_triple_quotes)
            {
                Debug.Log("Triple quotes found in the response.");
                // Example: Replace triple quotes with empty space or another string
                responseText = responseText.Replace("'''", "").Trim();
            }

            // Further processing of the response text
            if (responseText.EndsWith(":"))
            {
                responseText = responseText.Substring(0, responseText.Length - 1);
            }

            // Split and process the remaining text
            string[] lines_Temp = responseText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Debug.Log("Processed Response:");
            foreach (string line in lines_Temp)
            {
                Debug.Log(line);
            }
        }
        // Remove the colon that keeps showing up (Bug)
        if (responseText.EndsWith(":"))
        {
            responseText = responseText.Substring(0, responseText.Length - 1);
        }

        string[] lines = responseText.Trim().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].EndsWith("\\n"))
            {
                // Debug.Log("***************Trimmed lines from ChatGPT Response*******************\n\n" + lines[i] + "\n\n");
                int line_new_length = lines[i].Length - 2;
                lines[i] = lines[i].Substring(0, line_new_length).Trim();
            }
        }

        // Debug.Log("***************chatGPT Response Message***************");
        // Debug.Log(responseText);
        // Debug.Log("******************************************************");
        string[,] grid = new string[grid_width, grid_height];
        // Check that we have 10 lines to match our expected character_Grid size
        if (lines.Length == grid_width)
        {
            // Parse each line
            for (int i = 0; i < lines.Length; i++)
            {
                // Split the line into cells by the pipe character
                string[] cells = lines[i].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (cells.Length == 1)
                {
                    // The grid may not be separated by a '|', but a space ' '
                    // instead
                    cells = lines[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                }

                // Check that we have 10 cells in each line
                if (cells.Length == grid_width)
                {
                    for (int j = 0; j < cells.Length; j++)
                    {
                        // Trim the cell to get the ID and assign it to the character_Grid
                        string cellId;
                        if (cells[j].Contains('\n'))
                        {
                            cellId = cells[j].Trim('\n');
                        }
                        else
                        {
                            cellId = cells[j];
                        }
                        grid[i, j] = cellId;
                    }
                }
                else
                {
                    Debug.LogError("Unexpected number of cells in line: " + lines[i]);
                    // Exit if format is incorrect
                    return;
                }
            }

            // Debug.LogError("**************************character Grid**************************");
            // PrintGridToDebug(grid);
            // Debug.LogError("**************************************************************");
            // Now I have a character_Grid with IDs, you can instantiate game objects or tiles based on these IDs
            character_Grid = grid;
            PrintGridToDebug("New Character Grid", character_Grid);
            InstantiateCharacterGridPrefabs(grid, 0);
        }
        else
        {
            // This is for a bug I am getting sometimes. Every now and then, the length returns 11. 
            // Want to print out why when it happens
            Debug.Log("***ERROR: lines.Length == " + lines.Length + "***\n\n");
            // for (int i = 0; i < lines.Length; i++)
            // {
            //     Debug.Log(lines[i] + "\n");
            // }
        }
    }

    // Converts a 2D array to a string
    void PrintGridToDebug(string output_message, string[,] character_Grid)
    {
        string grid_output = "";
        string finalized_output_message = output_message + "\n";
        grid_output += finalized_output_message;
        // GetLength(0) returns the size of the first dimension
        for (int i = 0; i < character_Grid.GetLength(0); i++)
        {
            // GetLength(1) returns the size of the second dimension
            for (int j = 0; j < character_Grid.GetLength(1); j++)
            {
                // Append each cell's content to the string
                grid_output += character_Grid[i, j] + " ";
            }
            // Newline at the end of each row
            grid_output += "\n";
        }
        Debug.Log(grid_output);
    }

    // Not using currenlty
    // Helper method to update a character's position in the character_Grid
    private void UpdateCharacterPositionInGrid(string character_id, int newX, int newY)
    {
        // find and clear the character's current position
        for (int i = 0; i < character_Grid.GetLength(0); i++)
        {
            for (int j = 0; j < character_Grid.GetLength(1); j++)
            {
                if (character_Grid[i, j] == character_id)
                {
                    // Clear the current position
                    character_Grid[i, j] = "000";
                    break;
                }
            }
        }

        // Set the character's new position in the character_Grid?
        // character_Grid[newX, newY] = character_id;
    }

    // Function to send a prompt to ChatGPT and wait until a response is received
    public async Task<ChatResult> SendPromptToChatGPT(string prompt)
    {
        try
        {
            // Perform the chat completion request and wait for it to complete
            // IMPORTANT NOTE: If this is stuck, then it is most likely because 
            // I have run out of credits for my current chat gpt api. Go here to 
            // buy more: https://platform.openai.com/settings/organization/billing/overview
            var chat_gpt_result = await api.Chat.CreateChatCompletionAsync(new ChatRequest
            {
                Model = Model.GPT4_Turbo,
                Temperature = 0.7,
                MaxTokens = 500,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = ChatMessageRole.System, TextContent = prompt }
                }
            });

            // Return the text content of the response
            return chat_gpt_result;
        }
        catch (Exception error)
        {
            Debug.LogError($"API Error: {error}");
            return null;
        }
    }

    private string GridToString(string[,] character_Grid)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < character_Grid.GetLength(0); i++)
        {
            for (int j = 0; j < character_Grid.GetLength(1); j++)
            {
                // Append each cell's content to the StringBuilder, followed by a space for readability
                sb.Append(character_Grid[i, j] + " ");
            }
            // Append a newline at the end of each row
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public GameObject emptyPrefab;
    public GameObject grassPrefab;
    public GameObject waterPrefab;
    public GameObject rockPrefab;
    public GameObject civilianMan;
    public GameObject civilianWoman;
    public GameObject housePrefab;
    public GameObject flowerPrefab;
    public GameObject environmentTile;
    public GameObject GetPrefabById(string id)
    {
        switch (id.ToLower())
        {
            case "000": return emptyPrefab;
            case "001": return grassPrefab;
            case "002": return waterPrefab;
            case "003": return rockPrefab;
            case "004": return housePrefab;
            case "005": return flowerPrefab;
            case "101": return civilianMan;
            case "102": return civilianWoman;
            default: return null;
        }
    }

    // Reference to the most recent tile
    GameObject current_Tile;
    // Instantiates the map
    void InstantiateGrid(string[,] grid, int y_Pos)
    {
        // ClearInstantiatedTiles();
        // Debug.LogError("***Instanciating grid***\n\n");
        // PrintGridToDebug(grid);
        for (int i = 0; i < grid.GetLength(0); i++)
        {
            for (int j = 0; j < grid.GetLength(1); j++)
            {
                GameObject prefab = GetPrefabById(grid[i, j]);
                if (prefab != null)
                {
                    // This command makes a copy of the prefab object and adds it to the game world.
                    current_Tile = Instantiate(prefab, new Vector3(i, y_Pos, j), Quaternion.identity);
                    // Groups the tiles together and organizes them neatly
                    current_Tile.transform.parent = environmentTile.transform;
                }
                else
                {
                    Debug.Log("prefab = null");
                    Debug.Log(grid[i, j]);
                }
            }
        }
    }

    // For when I receive a new activity
    private void UpdateCharacterActivity(string character_id, string new_activity)
    {
        Debug.Log($"Trying to update character with ID: {character_id}, Task: {new_activity}");

        foreach (var character_tile in instantiated_player_tiles)
        {
            if (character_tile != null && character_tile.name == character_id)
            {
                Debug.Log($"Found character: {character_tile.name}, updating task.");

                // Access the CharacterScript component
                var character_script = character_tile.GetComponent<CharacterScript>();
                if (character_script != null)
                {
                    // Use CharacterScript to update activity text
                    character_script.UpdateActivity(new_activity);
                    Debug.Log($"Successfully updated character's task to: {new_activity}");
                }
                else
                {
                    Debug.LogError("CharacterScript not found on the character tile.");
                }
                return;  // Exit after updating the correct character
            }
        }
    }
    private Character GetCharacterDataById(string objectId)
    {
        if (characters_by_id.TryGetValue(objectId, out Character character))
        {
            return character;
        }
        // Return null if no character with the given ID exists
        return null;
    }

    // Reference to the most recent tile
    GameObject current_Tile_2;
    // Instantiates the map
    void InstantiateCharacterGridPrefabs(string[,] grid, int y_Pos)
    {
        ClearInstantiatedTiles();

        for (int i = 0; i < grid.GetLength(0); i++)
        {
            for (int j = 0; j < grid.GetLength(1); j++)
            {
                string cellData = grid[i, j];
                string characterId = cellData;
                string task = null;

                // Check if the cell contains a task (comma-separated)
                if (cellData.Contains(","))
                {
                    string[] splitData = cellData.Split(',');
                    characterId = splitData[0].Trim();  // Extract character ID
                    task = splitData[1].Trim();         // Extract the task
                }

                // Instantiate the prefab based on character ID
                GameObject prefab = GetPrefabById(characterId);
                if (prefab != null)
                {
                    GameObject characterInstance = Instantiate(prefab, new Vector3(i, 0, j), Quaternion.identity);
                    characterInstance.transform.parent = environmentTile.transform;

                    // Assign the correct ID to avoid mismatches during the search
                    characterInstance.name = characterId;

                    // Call the CharacterScript to update the activity
                    CharacterScript characterScript = characterInstance.GetComponentInChildren<CharacterScript>();
                    if (characterScript != null && task != null)
                    {
                        characterScript.UpdateActivity(task);
                    }

                    instantiated_player_tiles[i, j] = characterInstance;
                }
            }
        }
    }
    
    private void ClearInstantiatedTiles()
    {
        for (int i = 0; i < grid_width; i++)
        {
            for (int j = 0; j < grid_height; j++)
            {
                // Check if there's an instantiated GameObject at the current position
                if (instantiated_player_tiles[i, j] != null)
                {
                    // Destroy the GameObject in the scene
                    Destroy(instantiated_player_tiles[i, j]);
                    // Clear the reference for memory management
                    instantiated_player_tiles[i, j] = null;
                }
            }
        }
    }
}

/* 
            string prompt = "";
            if (current_prompt == Prompt_Selected.PS_Brief_Paragraph) {
                prompt =
                    "Construct the grid based on the description provided in the 'BackgroundStory' section " +
                    "of the environment_data.json file. Construct it using the tiles specified in the json file. Each " +
                    "EnvironmentTile has an associated ObjectID. Use this ObjectID in the construction of the grid. Make " +
                    "sure the 10x10 grid is represented in a text format suitable for parsing. Only provide the " +
                    "grid in your response. Format the grid as a table with 10 rows and 10 columns, " +
                    "where each cell contains a three-digit ObjectID of the tile which are provided in the " +
                    "'EnvironmentTiles' section. Separate each ID with a pipe '|' symbol and terminate each row " +
                    "with a newline character '\\n'" +
                    "Here is an example row: 001|001|001|001|001|001|001|001|001|001|n" +

                    "\n\n" +
                    "Here is the environment_data.json\n" + environment_data_string +
                    "\n\n" +

                    "Respond only with the 10x10 grid.";
                }
            else if (current_prompt == Prompt_Selected.PS_Descriptive_Paragraph)
            {
                prompt = "Construct a 10x10 grid of EnvironmentTiles, which are provided in the " +
                    "environment_data.json below, that is created based off the description provided" +
                    "in the 'BackgroundStory' section of the envirnoment_data.json. Construct the " +
                    "grid using the EnvironmentTiles that are specified in the json file. Each of the EnvironmentTiles " +
                    "has an associated ObjectID variable. Use this ObjectID to create the grid. Each ObjectID corresponds " +
                    "to a specific tile type. Make sure the 10x10 grid is represented in a text format suitable for " +
                    "parsing. Only provide the grid in your response, with no additional text or artifacts. Format " +
                    "the grid as a table with 10 rows and 10 columns, where each cell contains the three-digit ObjectID " +
                    "of the corresponding tile. Separate each ID with a pipe '|' symbol and terminate each row with a " +
                    "newline character '\\n'. Here is an example row: 001|001|001|001|001|001|001|001|001|001|\n" +

                    "Here is the environment_data.json file:\n" +

                    environment_data_string +

                    "Respond only with the 10x10 grid in the format specified.";
            }
            else if (current_prompt == Prompt_Selected.PS_Brief_List)
            {
                prompt =
                    "Instructions:\n" +
                    "   1. Construct a 10x10 grid of ObjectIDs that correspond to the EnvironmentTiles provided in the\n" +
                    "   environment_data.json file below.\n" +
                    "   2. The grid of ObjectIDs that correspond to the EnvironmentTiles should should be based off\n" +
                    "   of the 'BackgroundStroy' section of the environment_data.json file provided below.\n" +
                    "Grid Format:\n" +
                    "   1. The grid should have 10 row and 10 columns, where each cell contains the three-digit ObjectID\n" +
                    "   of the corresponding tile. Separate each ID with a pipe '|' symbol and terminate each row with a\n" +
                    "   2. newline character '\\n'.\n" +
                    "   3. Here is an example row: 001|001|001|001|001|001|001|001|001|001|\\n\n" +
                    "   4. It is very important that your response only includes the grid and no additional artifacts.\n\n" +
                    "environment_data.json:\n" +

                    environment_data_string + "\n" +

                    "Respond only with the 10x10 grid in the format specified.";
            }
            else if (current_prompt == Prompt_Selected.PS_Descriptive_List)
            {
                // Duplicate from PS_Brief_List because it is very effective so far
                prompt =
                    "Instructions:\n" +
                    "   1. Construct a 10x10 grid of ObjectIDs that correspond to the EnvironmentTiles provided in the\n" +
                    "   environment_data.json file below.\n" +
                    "   2. The grid of ObjectIDs that correspond to the EnvironmentTiles should should be based off\n" +
                    "   of the 'BackgroundStroy' section of the environment_data.json file provided below.\n" +
                    "Grid Format:\n" +
                    "   1. The grid should have 10 row and 10 columns, where each cell contains the three-digit ObjectID\n" +
                    "   of the corresponding tile. Separate each ID with a pipe '|' symbol and terminate each row with a\n" +
                    "   2. newline character '\\n'.\n" +
                    "   3. Here is an example row: 001|001|001|001|001|001|001|001|001|001|\\n\n" +
                    "   4. It is very important that your response only includes the grid and no additional artifacts.\n\n" +
                    "environment_data.json:\n" +

                    environment_data_string + "\n" +

                    "Respond only with the 10x10 grid in the format specified.";
            }
            else
            {
                Debug.Log("ERROR: Prompt not initialized.");
            }
------------------------------
        string prompt = "";
        if (current_prompt == Prompt_Selected.PS_Brief_Paragraph)
        {
            prompt =
                "Instructions: Place each character from the 'Characters' list in the provided JSON onto unique " +
                "positions in the 10x10 grid where the current tile ID in the 'Current World Grid' corresponds to " +
                "a walkable tile (i.e., 'Walkable': true in 'EnvironmentTiles'). Consider their roles and the environment " +
                "when choosing positions. Follow these rules:\n" +
                "'EnvironmentTiles'.\n" +
                "   - Match the position to the character's 'Role':\n" +
                "     - Place farmers directly on grass tiles.\n" +
                "     - Place fishers on walkable tiles adjacent to water tiles.\n" +
                "   - Ensure no two characters occupy the same position.\n" +
                "   - Ensure that characters are not on a tile that's walkable variable is marked as 'false'.\n" +
                " - **Tasks**: Assign each character a task by selecting one from their 'DayTasks' if the current time " +
                "is between 0600 and 1759, or from their 'NightTasks' if between 1800 and 0559.\n" +
                "   - The current time is " + time_of_day + ".\n" +
                " - **Grid Update**: For positions without characters, use the tile ID from the 'Current World Grid'.\n" +
                " - **Format**: Respond with a 10x10 grid where:\n" +
                "   - Cells with characters are 'CharacterID,Task' (e.g., '101,farming').\n" +
                "   - Cells without characters are the tile ID (e.g., '001').\n" +
                "   - Separate cells with '|' and end rows with '\\n'.\n" +
                "   - Example row: 001|001|001|101,gathering_resources|001|001|001|001|001|001|\n" +
                "Remember, DO NOT PLACE CHARACTERS ON TILES WHERE THE WALKABLE VARIABLE IS 'false'. For roles " +
                "requiring proximity to certain tiles (e.g., fishers near water), place them on adjacent walkable " +
                "tiles, not on the non-walkable tiles themselves.\n\n" +
                "Character Data: " + character_data_string + "\n\n" +
                "Environment Data: " + environment_data_string + "\n\n" +
                "Current World Grid: " + world_Grid_String + "\n\n" +
                "Remember, DO NOT PLACE CHARACTERS ON TILES WHERE THE WALKABLE VARIABLE IS 'false'.\n" + 
                "Respond only with the 10x10 grid.";
        }
        else if (current_prompt == Prompt_Selected.PS_Descriptive_Paragraph)
        {
             prompt =
                "Instructions: I've provided the current_world_grid below, which is a 10x10 grid of ObjectIDs. " +
                "The current_world_grid provided below represents the current world, which contains ObjectIDs that are used to " +
                "to represent EnvironmentTiles. The EnvironmentTiles and their associated ObjectIDs are located " +
                "in the environment_data.json file provided below. Your task is to place one character for each character " +
                "type that is specified in the character_data.json file provided below, onto the current_world_grid provided below by simply " +
                "replacing the EnvironmentTile's ObjectID with the associated character's ObjectID. Each " +
                "EnvironmentTile in the environment_data.json file provided below has a variable called 'walkable' which indicates " +
                "whether or a not a character's ObjectID can replace a EnvironmentTile's objectID. Make sure the " +
                "EnvironmentTile's 'walkable' variable is set to true before you replace the EnvironmentTile's " +
                "ObjectID with the character's ObjectID. If the EnvironmentalTile's 'walkable' variable is false, " +
                "then that means the character's ObjectID cannot replace the EnvironmentTile's ObjectID. In addition " +
                "to that, I would like you to also specify a role for that character in this format: " +
                "'CharacterID,Task' (e.g., '101,farming'). Make sure the role you specify for each character " +
                "is a role that is explicitly stated in the 'DayTasks' section of the character_data.json provided below or the " +
                "'NightTasks' section of the character_data.json provided below for the associated character you are placing. " +
                "The current time is " + formatted_time + ". If the time is between 0600 and 1759, make sure and use a " +
                "task that is from the 'DayTasks' section and if there are multiple tasks specified in the 'DayTasks' " +
                "section of the character_data.json provided below file, pick one that is most relevant to the given situation and " +
                "position of the character in the current world. If the time is between 1800 and 0559, " +
                "make sure and use a task that is from the 'NightTasks' section of the character_data.json file provided below" +
                "and if there are multiple tasks specified in the 'NightTasks' section of the character_data.json file provided below," +
                "pick one that is most relevant to the given situation and position of the character in the current " +
                "world. Separate each ID with a pipe '|' symbol and terminate each row with a newline character. Make sure you only " +
                "provide the 10x10 grid of ObjectIDs that are separated by the pipe '|' symbol and terminated each row with a " +
                "newline character in your response. Here is a example row for your reference: " +
                "001|001|001|101,farming|001|001|001|001|001|001|.\n " +

                "Here is the environment_data.json file:\n" +
                environment_data_string + "\n\n" +

                "Here is the character_data.json file:\n" +
                character_data_string + "\n\n" +

                "Here is the current_world_grid:\n" + 
                world_Grid_String + "\n\n" +

                "Respond only with the 10x10 grid in the format specified.";
        } else if (current_prompt == Prompt_Selected.PS_Brief_List) { 
            prompt = 
                "Instructions: " +
                    "1. Place characters from `character_data.json` onto the `current_world_grid`.\n" +
                    "2. For each character:\n" +
                    "   - Find their ObjectID (e.g., 101 for Civilian Man, 102 for Civilian Woman)\n" +
                    "   - Check the `EnvironmentTiles` in `environment_data.json` to identify walkable tiles (`Walkable: true`).\n" +
                    "   - **Only replace walkable tiles** (ObjectIDs 001 or 003) with the character’s ObjectID.\n" +
                    "   - Assign a task from `DayTasks`/`NightTasks` based on the current time (2000 → use `NightTasks`).\n" +
                    "3. Example:\n" +
                    "   - \"Role\" : \"Farmer\" (101) should be placed on grass (001) with a task like \"resting\".\n" +
                    "   - \"Role\" : \"Fisher\" (102) should be placed near water (002) but **not on water** (since it’s unwalkable).\n" +
                    "Format:\n" +
                    "- Replace ONE walkable tile per character.\n" +
                    "- Use `CharacterID,Task` format (e.g., `101,resting`).\n" +
                    "- Separate cells with `|` and rows with `\\n`. " + 
                    "JSON Data:\n" +

                    "Here is the environment_data.json file:\n" +
                    environment_data_string + "\n\n" +

                    "Here is the character_data.json file:\n" +
                    character_data_string + "\n\n" +

                    "Here is the current_world_grid:\n" + 
                    world_Grid_String + "\n\n" +

                    "Respond only with the 10x10 grid in the format specified."; 

        } else if (current_prompt == Prompt_Selected.PS_Descriptive_List) {
            prompt =
                "Instructions:\n" +
                "   1. Construct a 10x10 grid that places once character in the grid for each character specified in the \n" +
                "   character_data.json provided below.\n" +
                "   2. Below I have provided the current_world_map as it exists. This map contains the ObjectIDs of different\n" +
                "   EnvironmentTiles that correspond to a specific tile type that is specified in the environment_data.json\n" +
                "   file provided below. This map represents the world the characters stand on top of.\n" +
                "   3. Your task is to replace some of the ObjectIDs of the tiles in the current_world_map with character\n" +
                "   ObjectIDs. Place once character for each character that is specified in the charater_data.json file.\n" +
                "   4. Take note of the 'walkable' variable inside of the environoment_data.json file provided below when\n" +
                "   replacing a tile with a character tile.\n" +
                "   This value specifies if that tile can be replaced by a character. It is VERY\n" +
                "   IMPORTANT that the EnvironmentTile ObjectID you replace with the character you are placing has a\n" +
                "   'walkable variable that is marked 'true', indicating the tile can be replaced by a character\n" +
                "   and walked on. This will represent the position of the character in the world.\n" +
                "   5. Make sure you place the character in a relevant position in the world. Look at the characters role\n" +
                "   and see if you can have their position reflect their role. (Example: If the character is a fisher, put\n" +
                "   them near water).\n" +
                "   6. Provide a task the current character by providing the task type using this format\n" +
                "   'CharacterID,Task'. An example would be '101,fishing'. Make sure the task you set is from one of the\n" +
                "   DayTasks or NightTasks that are specified in the character_data.json file. The current time of day is\n" +
                "   " + time_of_day + ". If the time, which is in military time, is between 0600 and 1800, use one of the\n" +
                "   character's DayTasks. If it is between 1800 and 0600, use a NightTask.\n" +
                "Grid Format:\n" +
                "   1. The grid should have 10 row and 10 columns, where each cell contains the three-digit ObjectID\n" +
                "   2. of the corresponding tile in the current_world_grid, or the newly placed characters ObjectID.\n" +
                "   3. Separate each ID with a pipe '|' symbol and terminate each row with a newline character '\\n'.\n" +
                "   4. Here is an example row: 001|001|001|001|001|101,fishing|001|001|001|001|\\n\n" +
                "   5. It is very important that your response only includes the grid and no additional artifacts.\n\n" +

                "environment_data.json:\n" +
                environment_data_string + "\n\n" +

                "character_data.json:\n" +
                character_data_string + "\n\n" +

                "current_world_grid:\n" + 
                world_Grid_String + "\n\n" +

                "Respond only with the 10x10 grid in the format specified." ;
        } else
        {
            Debug.Log("ERROR: Prompt not initialized.");
        }

------------------------------
            string prompt = "";
            if (current_prompt == Prompt_Selected.PS_Brief_Paragraph)
            {
                prompt =
                    "Instructions: Update the positions and tasks of each character in the 'Current Character Grid' " +
                    "on a 10x10 grid. Follow these rules:\n" +
                    " - **Movement**: For each character in the 'Current Character Grid':\n" +
                    "   - Attempt to move them one block (up, down, left, or right) to an adjacent tile that is walkable " +
                    "(where 'Walkable' is true in 'EnvironmentTiles') and not occupied by another character.\n" +
                    "   - If a valid move is possible, move the character and replace their previous position with the " +
                    "tile ID from the 'Original World Grid'. If no valid move is available, keep them in their current" +
                    " position.\n" +
                    "   - Ensure all characters occupy unique tiles after movement.\n" +
                    " - **Tasks**: Assign each character a task based on their 'Role' and the tile they are on or adjacent " +
                    "to:\n" +
                    "   - Use 'DayTasks' if the current time (" + time_of_day + ") is 0600-1759, or 'NightTasks' if " +
                    "1800-0559.\n" +
                    "   - Example: Farmers get 'farming' on grass tiles, fishers get 'fishing' near water tiles.\n" +
                    " - **Grid Update**: For tiles without characters, use the tile ID from the 'Original World Grid'.\n" +
                    " - **Format**: Respond only with a 10x10 grid where:\n" +
                    "   - Cells with characters are 'CharacterID,Task' (e.g., '101,farming').\n" +
                    "   - Cells without characters are the tile ID (e.g., '001').\n" +
                    "   - Separate cells with '|' and end rows with '\\n'.\n" +
                    " - **Note**: Input grids ('Original World Grid' and 'Current Character Grid') are space-separated, " +
                    "but the response must use '|'.\n\n" +
                    
                    "Character Data: " + character_data_string + "\n\n" +
                    "Environment Data: " + environment_data_string + "\n\n" +
                    "Original World Grid: " + world_Grid_String + "\n\n" +
                    "Current Character Grid: " + character_Grid_String + "\n\n" +
                    
                    "Remember, DO NOT PLACE CHARACTERS ON TILES WHERE THE WALKABLE VARIABLE IS 'false'" +
                    "Respond only with the 10x10 grid.";
            }
            else if (current_prompt == Prompt_Selected.PS_Descriptive_Paragraph)
            {
                prompt =
                    "Instructions: I've provided the current_world_grid below, which is a 10x10 grid of ObjectIDs. " +
                    "The current_world_grid provided below represents the current world, which contains ObjectIDs that are used to " +
                    "to represent EnvironmentTiles. The EnvironmentTiles and their associated ObjectIDs are located " +
                    "in the environment_data.json file provided below. Also below, I've included the current_character_grid which " +
                    "shows the current position of the players inside of the current_world_grid. When a character is moved on the map," +
                    "they replace the ObjectID of the EnvironmentTile's ObjectIDs. Your task is to update the positions and the roles " +
                    "of the characters in the map. When updating the positions of the characters, make sure you only move the characters " +
                    "one tile " +
                    "in any direction of your choosing (North, South, East, West). The character_data.json file contains information " +
                    "about the characters. When moving the characters. Please move them in a relevant direction, if applicable, based " +
                    "off what their task is and based off the BackgroundStory that is specified in the environment_data.json file that " +
                    "is specified below. When moving a character, make sure to replace the EnviromentalTile's ObjectID that the character " +
                    "is moving to with the ObjectID of the character that is moving. Each " +
                    "EnvironmentTile in the environment_data.json file provided below has a variable called 'walkable' which indicates " +
                    "whether or a not a character's ObjectID can replace a EnvironmentTile's objectID. Make sure the " +
                    "EnvironmentTile's 'walkable' variable is set to true before you replace the EnvironmentTile's " +
                    "ObjectID with the character's ObjectID. If the EnvironmentalTile's 'walkable' variable is false, " +
                    "then that means the character's ObjectID cannot replace the EnvironmentTile's ObjectID. In addition " +
                    "to that, I would like you to also specify a role for that character in this format: " +
                    "'CharacterID,Task' (e.g., '101,farming'). Make sure the role you specify for each character " +
                    "is a role that is explicitly stated in the 'DayTasks' section of the character_data.json provided below or the " +
                    "'NightTasks' section of the character_data.json provided below for the associated character you are placing. " +
                    "The current time is " + formatted_time + ". If the time is between 0600 and 1759, make sure and use a " +
                    "task that is from the 'DayTasks' section and if there are multiple tasks specified in the 'DayTasks' " +
                    "section of the character_data.json provided below file, pick one that is most relevant to the given situation and " +
                    "position of the character in the current world. If the time is between 1800 and 0559, " +
                    "make sure and use a task that is from the 'NightTasks' section of the character_data.json file provided below" +
                    "and if there are multiple tasks specified in the 'NightTasks' section of the character_data.json file provided below," +
                    "pick one that is most relevant to the given situation and position of the character in the current " +
                    "world. Separate each ID with a pipe '|' symbol and terminate each row with a newline character. Make sure you only " +
                    "provide the 10x10 grid of ObjectIDs that are separated by the pipe '|' symbol and terminated each row with a " +
                    "newline character in your response. Here is a example row for your reference: " +
                    "001|001|001|101,farming|001|001|001|001|001|001|.\n " +

                    "Here is the environment_data.json file:\n" +
                    environment_data_string + "\n\n" +

                    "Here is the character_data.json file:\n" +
                    character_data_string + "\n\n" +

                    "Here is the current_character_grid:\n" +
                    character_Grid_String + "\n\n" +

                    "Here is the current_world_grid:\n" +
                    world_Grid_String + "\n\n" +

                    "Respond only with the 10x10 grid in the format specified.";
            }
            else if (current_prompt == Prompt_Selected.PS_Brief_List)
            {
                prompt =
                   "Instructions:\n" +
                   "1. For each character in the 10x10 `current_character_grid` provided below:\n" +
                   "   - Check their current position and task (e.g., 101 is the objectID and \"resting\" is the task).\n" +
                   "   - Identify adjacent tiles (N/S/E/W) using `current_world_grid` and move the characters at most one tile in any direction (N/S/E/W)." +
                   "     that is relevant to their given task and position.\n" +
                   "   - Validate movement using `EnvironmentTiles` in `environment_data.json`:\n" +
                   "     - Target tile must be walkable (`walkable: true`).\n" +
                   "     - Do NOT move into any of the following tiles: " + GetNonWalkableTiles(environment_data_string) + ".\n" +
                   "2. Update tasks based on time (0000 → use `NightTasks`).\n" +
                   "   - Farmer (101): If near house (004), keep \"resting\".\n" +
                   "   - Fisher (102): If near water (002), keep \"resting\".\n" +
                   "3. If no valid moves, leave the character in place.\n" +
                   "Format:\n" +
                   "- Replace ONE walkable tile per character.\n" +
                   "- Use `CharacterID,Task` format (e.g., `101,resting`).\n" +
                   "- Separate cells with `|` and rows with `\\n`. " +
                   "- Make sure the grid is at most a length of 10x10." +

                   "JSON Data:\n" +

                   "Here is the environment_data.json file:\n" +
                    environment_data_string + "\n\n" +

                   "Here is the character_data.json file:\n" +
                    character_data_string + "\n\n" +

                   "Here is the current_character_grid:\n" +
                    character_Grid_String + "\n\n" +

                   "Here is the current_world_grid:\n" +
                    world_Grid_String + "\n\n" +

                   "Respond ONLY with the updated 10x10 grid with the characters moving one tile in any direction..\n";
            }
            else if (current_prompt == Prompt_Selected.PS_Descriptive_List)
            {
                prompt =
                    "Instructions:\n" +
                    "   1. Construct a 10x10 grid that updates the position of a character in the current_character_grid\n" +
                    "   provided below.\n" +
                    "   2. Below I have provided the current_world_map as it exists. This map contains the ObjectIDs of different\n" +
                    "   EnvironmentTiles that correspond to a specific tile type that is specified in the environment_data.json " +
                    "   file provided below. This map represents the world the characters stand on top of.\n" +
                    "   3. Below, I have also provided the current_character_grid which shows where the characters are\n" +
                    "   currently positioned in the world." +
                    "   4. The current time is " + time_of_day + ". If the current time, which is in military time, is between\n" +
                    "   0600 and 1800, then move the characters ObjectID one tile in any direction. Either up one tile, down one tile,\n" +
                    "   left one tile, or right one tile if possible.\n" +
                    "   5. Once you move the character, replace the EnvironmentTile's OjbectID the move from with the\n" +
                    "   EnvironmentTile's ObjectID that is specified in the current_world_grid.\n" +
                    "   6. When you move the characters ObjectID, replace one of the tiles in the current_world_map with a character\n" +
                    "   ObjectID. Please take note of the 'walkable' variable inside of the environoment_data.json file provided\n" +
                    "   below. This value specifies if that tile can be walked on (or replaced by) a character. It is very\n" +
                    "   important that the EnvironmentTile ObjectID you replace with the character you are placing has a\n" +
                    "   'walkable variable that is marked 'true', indicating the tile can be replaced by a character\n" +
                    "   and walked on. This will represent the position of the character in the world. Also, Make sure you\n" +
                    "   move the character in a relevant direction in the world. Look at the characters role and see if you\n" +
                    "   can have their position reflect their role. (Example: If the character is a fisher, move them around water).\n" +
                    "   7. Lastly, please provide an updated task the current character by providing the task type using this format\n" +
                    "   'CharacterID,Task'. An example would be '101,fishing'. Make sure the task you set is from one of the\n" +
                    "   DayTasks or NightTasks that are specified in the character_data.json file. The current time of day is " +
                    "   " + time_of_day + ". If the time, which is in military time, is between 0600 and 1800, use one of the " +
                    "   character's DayTime tasks. If it is between 1800 and 0600, use a NightTask." +
                    "Grid Format:\n" +
                    "   1. The grid should have 10 row and 10 columns, where each cell contains the three-digit ObjectID\n" +
                    "   2. of the corresponding tile in the current_world_grid, or the newly placed characters ObjectID.\n" +
                    "   3. Separate each ID with a pipe '|' symbol and terminate each row with a newline character '\\n'.\n" +
                    "   4. Here is an example row: 001|001|001|001|001|101,fishing|001|001|001|001|\\n\n" +
                    "   5. It is very important that your response only includes the grid and no additional artifacts.\n\n" +

                    "environment_data.json:\n" +
                    environment_data_string + "\n\n" +

                    "character_data.json:\n" +
                    character_data_string + "\n\n" +

                    "current_world_grid:\n" +
                    world_Grid_String + "\n\n" +

                    "current_character_grid:\n" +
                    character_Grid_String + "\n\n" +

                    "Respond only with the 10x10 grid in the format specified.";
            }
            else
            {
                Debug.Log("ERROR: Prompt not initialized.");
            }

*/