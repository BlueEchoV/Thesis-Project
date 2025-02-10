using OpenAI_API;
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

public class OpenAIController : MonoBehaviour {
    // public TMP_Text textField;
    // public TMP_InputField inputField;
    // public Button okButton;

    private OpenAIAPI api;
    private List<ChatMessage> messages;

    // public bool isDebug = true;

    public const int grid_width = 10;
    public const int grid_height = 10;

    private GameObject[,] instantiated_player_tiles = new GameObject[grid_width, grid_height];
    // For the text rendering of the tasks
    // Dictionary is a collection of key-value pairs
    private Dictionary<string, Character> characters_by_id = new Dictionary<string, Character>();

    // Create a 10x10 character_Grid to hold the IDs (2D array)
    string backstory_global;
    string[,] world_grid_global = new string[grid_width, grid_height];
    string[,] character_Grid = new string[grid_width, grid_height];
    string walkable_block_ids;
    // Not using currently
    // static string gridGenerationInstructions = "Please generate a 10x10 grid map represented in a text format suitable for parsing. Only provide the map in your response. Format the map as a table with 10 rows and 10 columns, where each cell contains a three-digit ID of the tile or character which are provided in the environmentTiles and characters section. Separate each ID with a pipe '|' symbol and terminate each row with a newline character '\\n'. Include a colon at the end of the entire map. An example row might look like '001|002|003|...|010\\n', and there should be 10 such rows to complete the grid.";

    string environment_data_string;

    void Start()
    {
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
        string character_data_updating_file_path = Path.Combine(Application.dataPath, "character_data_updating.json");
        if (character_data_updating_file_path == null) {
            Debug.Log("ERROR: Filepath is null");
        }
        await PlaceCharactersInWorldAndUpdate(character_data_initial_file_path, character_data_updating_file_path); 
    }
    public class EnvironmentData 
    {
        public string BackgroundStory { get; set; }
        public FormatSpecification Format { get; set; }
        //     "GameTypeAndMechanics": "002",
        // public string GameTypeAndMechanics { get; set; }
        public List<EnvironmentTile> EnvironmentTiles { get; set; }
    }
    public class CharacterData {
        public FormatSpecification Format { get; set; } 
        public List<Character> Characters { get; set; }
    }
    public class FormatSpecification {
        public string Type { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public string Description { get; set; }
    }

    public class EnvironmentTile
    {
        public string ObjectID { get; set; }
        public string EnvironmentModelName { get; set; }
        public string Type { get; set; }
        public bool Walkable { get; set; }
    }

    public class Character
    {
        public string ObjectID { get; set; }
        public string Type { get; set; }
        public string CharacterModelName { get; set; }
        public string CharacterDescription { get; set; }
        public string CurrentActivity { get; set; }
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
        // Check if environment_data is null
        if (environment_data == null)
        {
            Debug.LogError("Environment data is null in GetWalkableBlocksString.");
            return string.Empty;
        }

        // Check if EnvironmentTiles is null
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


    // Load the character JSON file
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
        // Load the character JSON file
    private FormatSpecification LoadFormatSpecificationFromJson(string file_path)
    {
        // Read the text from the specified file
        string jsonContent = File.ReadAllText(file_path);
        // Convert the text into a CharacterData object
        var format_specification = JsonConvert.DeserializeObject<FormatSpecification>(jsonContent);
        return format_specification;
    }

    // NOTE: FIRST GENERATION PROMPT
    private async Task GenerateWorldWithChatGPT(string file_path)
    {
        // Debug.Log("Inside GenerateWorldWithChatGPT");
        if (File.Exists(file_path)) {
            EnvironmentData environment_data = LoadEnvironmentDataFromJson(file_path);
            backstory_global = JsonConvert.SerializeObject(environment_data.BackgroundStory, Formatting.None);
            Debug.Log("Back Story Global\n" + backstory_global);
            // NOTE: Convert the EnvironmentData type back into a json string
            environment_data_string = JsonConvert.SerializeObject(environment_data, Formatting.None);
            // For this prompt response, we only want it to generate the terrain (ignoring the character ids
            string prompt = 
                "Instructions: Construct a 10x10 grid using only the ObjectIDs from the JSON file provided. " +
                "Respond only with the grid, formatted as described in the 'Format' section. " +
                $"JSON File: {environment_data_string}";            

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
    private async Task PlaceCharactersInWorldAndUpdate(string file_path_initial_placement, string file_path_updating_placement)
    {
        // Debug.Log("Inside PlaceCharactersInWorldCoroutine");
        CharacterData initial_placement = LoadCharacterDataFromJson(file_path_initial_placement);
        string character_data_string = JsonConvert.SerializeObject(initial_placement, Formatting.None);

        string world_Grid_String = GridToString(world_grid_global);
        string character_Grid_String = GridToString(character_Grid);
        string prompt =
            "Instructions: Use the provided 'Character Data' to place each character on the Current World Grid " +
            "by replacing a Walkable Tile with the appropriate character's ID. Don't place them on a water tile! " +
            "Assign an appropriate task to each character based on the character description, back story, and environment. " +
            "Only place characters on the specified Walkable Tiles as defined in the Environment Tile Data. " +
            "If a cell does not contain a character, leave it as the environment tile ID only (e.g., '001'). " +
            "Format the response as a 10x10 grid where cells containing characters use the format 'CharacterID,Task'. " +
            "Here is an example row: 001|001|001|101,gathering_resources|001|001|001|001|001|001|\n\n" +

            "'Character Data':\n" + character_data_string + "\n\n" +
            "'Current World Grid':\n" + world_Grid_String + "\n\n" +
            "'Walkable Tiles' are dynamically determined by tiles where 'Walkable: true' in the Environment Tile Data.\n" +
            "'Environment Tile Data':\n" + environment_data_string + "\n\n" +
            "'Back Story':\n" + backstory_global + ".\n\n" +
            "Ensure that you do not place characters on any tiles where 'Walkable' is false in the Environment Tile Data.";

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
            StartCoroutine(UpdateCharacterPositionsCoroutine(file_path_updating_placement, initial_placement.Characters));
        }
    }

    // NOTE: THIRD GENERATION PROMPT (RECURRING)
    private IEnumerator UpdateCharacterPositionsCoroutine(string file_path_updating_placement, List<Character> characters)
    {
        // Construct the prompt with the static back story, current character_Grid, and character IDs
        // I destroy the previous grid here as well
        FormatSpecification updating_format_specification = LoadFormatSpecificationFromJson(file_path_updating_placement);
        string updating_format_specification_string = JsonConvert.SerializeObject(updating_format_specification , Formatting.None);
        // Debug.Log("updating_format_specification_string:\n" + updating_format_specification_string);

        string character_IDs = JsonConvert.SerializeObject(characters, Formatting.None);
        // Debug.Log("Character_IDs\n" + character_IDs);

        string world_Grid_String = GridToString(world_grid_global);

        // Start count at 3 because of two prompts prior
        int count = 3;
        // GAMELOOP
        while (true)
        {
            string character_Grid_String = GridToString(character_Grid);

            string prompt =
                "Update the character grid and assign the most suitable task to each character based on their type and environment context. " +
                "Move each character one block in any walkable direction (up, down, left, or right) if possible, and assign a task using the format: " +
                "CharacterID,Task. Ensure that tasks are meaningful based on the character’s type (e.g., a farmer could be assigned farming if near a crop tile). " +
                "If a character can't move, keep them in their current position but still assign them a task. " +
                "Replace any position a character moves from with the corresponding environment tile from the original world grid.\n\n" +
                "Walkable Tile IDs: Tiles with 'Walkable: true' in the JSON environment data should be treated as walkable.\n\n" +
                $"Character ID's: {character_IDs}\n" +
                $"Environment Tile Data (JSON): {environment_data_string}\n" +
                $"Original World Grid (without characters): {world_Grid_String}\n" +
                $"Current Character Grid (with characters on map): {character_Grid_String}\n" +
                "Respond **ONLY** with the updated 10x10 grid. Use the format CharacterID,Task in cells with characters, and only the tile ID in cells without characters. " +
                "Example format for a row: '101,fishing|002|003|102,building|...'";
            /*
            string prompt = "ONLY respond with the 10x10 grid in the format specified below:\n " +
                "Do not include any additional text, explanations, or comments. Move each character " +
                "one block in any walkable direction (up, down, left, or right) by replacing a Walkable" +
                "Tile with the appropriate character's ID based on the walkable " +
                "tiles in the original world grid. Don't place them on a water tile! 002" +
                $"Walkable tiles have the following IDs: {walkable_block_ids}. " +
                "If a character can't move, leave them in their current position. If a character is not " +
                "on the grid, place them randomly on a walkable tile. Replace any position a character " +
                "moves from with the corresponding environment tile from the original world grid. " +
                "Here is the data you need:\n" +
                $"Updating Character Positions: {updating_format_specification_string}\n" +
                $"Character ID's: {character_IDs}\n" +
                $"Original World Grid (without characters): {world_Grid_String}\n" +
                $"Current Character Grid (with characters on map): {character_Grid_String}\n" +
                "Respond **ONLY** with the updated 10x10 grid. Use the same format as the original " +
                "world grid, maintaining the environment tiles in any cells without characters. " +
                "Format each row using three-digit IDs separated by pipes ('|'), like this:" +
                " '001|002|003|...|010\\n'. " +
                "**Do not add any extra text**, and ensure the response is formatted exactly as specified.";
            */

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
        // ******************************************************************************

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
            // PrintGridToDebug("New Character Grid", character_Grid);
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
    public GameObject marketPrefab;
    public GameObject environmentTile;
    public GameObject GetPrefabById(string id)
    {
        switch (id.ToLower())
        {
            case "000": return emptyPrefab;
            case "001": return grassPrefab;
            case "002": return waterPrefab;
            case "003": return rockPrefab;
            case "004": return marketPrefab;
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
            Debug.Log(grid.GetLength(0));
            for (int j = 0; j < grid.GetLength(1); j++)
            {
                Debug.Log(grid.GetLength(1));
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
                    // Instantiate character at the grid position
                    current_Tile_2 = Instantiate(prefab, new Vector3(i, y_Pos, j), Quaternion.identity);
                    current_Tile_2.transform.parent = environmentTile.transform;

                    // Set the GameObject's name to the character ID only
                    current_Tile_2.name = characterId;

                    // Update character activity and display task if one is present
                    if (task != null)
                    {
                        Debug.Log($"Assigning task '{task}' to character '{characterId}'");
                        UpdateCharacterActivity(characterId, task);
                    }

                    // Store the tile reference
                    instantiated_player_tiles[i, j] = current_Tile_2;
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
