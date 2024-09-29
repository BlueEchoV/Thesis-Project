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

public class OpenAIController : MonoBehaviour
{
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
    string[,] world_grid_global = new string[grid_width, grid_height];
    string[,] character_Grid = new string[grid_width, grid_height];
    // Not using currently
    // static string gridGenerationInstructions = "Please generate a 10x10 grid map represented in a text format suitable for parsing. Only provide the map in your response. Format the map as a table with 10 rows and 10 columns, where each cell contains a three-digit ID of the tile or character which are provided in the environmentTiles and characters section. Separate each ID with a pipe '|' symbol and terminate each row with a newline character '\\n'. Include a colon at the end of the entire map. An example row might look like '001|002|003|...|010\\n', and there should be 10 such rows to complete the grid.";

    void Start() {
        api = new OpenAIAPI(Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User));
        // Find the JSON file (Application.dataPath points to the location where the game's data is stored)
        // Creates a file path to the JSON. Fore example: asset\background_Settings.json
        string file_path = Path.Combine(Application.dataPath, "background_Settings.json");
        GenerateWorldWithChatGPT(file_path);
        // StartCoroutine(PlaceCharactersInWorldCoroutine(file_path));
    }

    public class GameData {
       public string BackgroundStory { get; set; }
       public FormatSpecification Format { get; set; }
        //     "GameTypeAndMechanics": "002",
        // public string GameTypeAndMechanics { get; set; }
        public List<EnvironmentTile> EnvironmentTiles { get; set; }
        public List<Character> Characters { get; set; }
    }
    public class FormatSpecification {
        public string Type { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public string Description { get; set; }
    }

    public class EnvironmentTile {
        public string ObjectID { get; set; }
        public string EnvironmentModelName { get; set; }
        public string Type { get; set; }
    }

    public class Character {
        public string ObjectID { get; set; }
        public string CharacterModelName { get; set; }
        public string CharacterDescription { get; set; }
        public string Type { get; set; }
        public string CurrentActivity { get; set; }
    }

    private GameData LoadGameDataFromJson(string file_path) {
        // NOTE: Grab the text from the specified tile 
        string jsonContent = File.ReadAllText(file_path);
        // NOTE: Convert the text into a object (GameData)
        var gameData = JsonConvert.DeserializeObject<GameData>(jsonContent);
        // NOTE: Set the key value pairs for the dictionary so we can refer back
        // to the character data by id 
        // NOTE: For the text rendering
        foreach (var character in gameData.Characters) {
            characters_by_id[character.ObjectID] = character;
        }
        return gameData;
    }

    private async void GenerateWorldWithChatGPT(string file_path) {
        Debug.Log("Inside GenerateWorldWithChatGPT");
        if (File.Exists(file_path)) {
            GameData gameData = LoadGameDataFromJson(file_path);
            // NOTE: Convert the GameData type back into a json string
            string game_data_string = JsonConvert.SerializeObject(gameData, Formatting.None);
            // For this prompt response, we only want it to generate the terrain (ignoring the character ids
            string prompt = "Instructions: Only respond with the grid." +
                            // String interpolation '$'
                            $"Here is the JSON file containing the game information and IDs: {game_data_string}";
           
            Debug.Log("Message sent to chatGPT:\n" + prompt);

            var chat_gpt_result = await api.Chat.CreateChatCompletionAsync(new ChatRequest
            {
                Model = Model.GPT4,
                // NOTE: Temperature is level of creativity of ChatGPT. Higher is more varied
                // but creative, and lower is less creative but more predictable
                Temperature = 0.7,
                MaxTokens = 500,
                // NOTE: The conversation history
                Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.System, TextContent = prompt }
            }
            });

            // NOTE: Process the response from ChatGPT
            if (chat_gpt_result != null && chat_gpt_result.Choices != null && chat_gpt_result.Choices.Count > 0) {
                // NOTE: Pull the text from the ChatResult struct
                string chat_gpt_response = chat_gpt_result.Choices[0].Message.TextContent;
                InstantiateWorldGrid(chat_gpt_response);
            }
            else {
                Debug.LogError("ChatResult is null or does not contain choices.");
            }
        }
        else {
            Debug.LogError("Cannot find the JSON file: " + file_path + "\n\n");
        }
    }

    void InstantiateWorldGrid(string chat_gpt_response) {
        // Debug.Log("Inside InstantiateWorldGrid");

        // NOTE: This processes the response from chat GPT to account for 
        // the error responses it has been giving.
        // NOTE: THIS HAS TO BE MORE DYNAMIC
        const string unwanted_phrase = "Here is the 10x10 grid representing a grass field:";
        bool contains_unwanted_phrase = chat_gpt_response.Contains(unwanted_phrase);
        bool contains_unwanted_triple_quotes = chat_gpt_response.Contains("'''");
        if (contains_unwanted_phrase || contains_unwanted_triple_quotes) {
            if (contains_unwanted_phrase) {
                Debug.Log("Trimming unwanted phrase found in chatGPT's response.");
                int phrase_index = chat_gpt_response.IndexOf(unwanted_phrase) + unwanted_phrase.Length;
                chat_gpt_response = chat_gpt_response.Substring(phrase_index).Trim();
            }

            if (contains_unwanted_triple_quotes) {
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
        if (chat_gpt_response.EndsWith(":")) {
            chat_gpt_response = chat_gpt_response.Substring(0, chat_gpt_response.Length - 1);
        }

        string[] lines = chat_gpt_response.Trim().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++) {
            // Debug.Log("***************Trimmed lines from ChatGPT Response*******************\n\n" + lines[i] + "\n\n");
        }
        // Debug.Log("***************InstantiateWorldGrid: GPT Response***************");
        // Debug.Log(chat_gpt_response);
        // Debug.Log("******************************************************");
        string[,] grid = new string[grid_width, grid_height];
        // Check that we have 10 lines to match our expected character_Grid size
        if (lines.Length == grid_width) {
            // Parse each line
            for (int i = 0; i < lines.Length; i++) {
                // Split the line into cells by the pipe character
                string[] cells = lines[i].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                // Check that we have 10 cells in each line
                if (cells.Length == grid_width) {
                    for (int j = 0; j < cells.Length; j++) {
                        // Trim the cell to get the ID and assign it to the character_Grid
                        string cell_id = cells[j].Trim();
                        grid[i, j] = cell_id;
                    }
                }
                else {
                    Debug.LogError("ERROR: Unexpected number of cells in line: " + lines[i]);
                    // PrintGridToDebug(grid);
                    // Debug.LogError("**************************************************************");
                    // Exit if format is incorrect
                    return;
                }
            }

            PrintGridToDebug("First Grid instantiation", grid);
            // Now I have a character_Grid with IDs, you can instantiate game objects or tiles based on these IDs

            world_grid_global = grid;
            InstantiateGrid(grid, 0);
        }
        else {
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

            // Optionally handle the triple quotes in a specific manner
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
            // Debug.Log("***************Trimmed lines from ChatGPT Response*******************\n\n" + lines[i] + "\n\n");
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
                        string cellId = cells[j].Trim();
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
            // Debug.LogError("**************************character Grid after assignment**************************");
            // PrintGridToDebug(character_Grid);
            // Debug.LogError("**************************************************************");
            InstantiateCharacterGridPrefabs(grid, 0);
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

    // Converts a 2D array to a string
    void PrintGridToDebug(string output_message, string[,] character_Grid) {
        string grid_output = "";
        string finalized_output_message = output_message + "\n";
        grid_output += finalized_output_message;
        // GetLength(0) returns the size of the first dimension
        for (int i = 0; i < character_Grid.GetLength(0); i++) {
            // GetLength(1) returns the size of the second dimension
            for (int j = 0; j < character_Grid.GetLength(1); j++) {
                // Append each cell's content to the string
                grid_output += character_Grid[i, j] + " ";
            }
            // Newline at the end of each row
            grid_output += "\n"; 
        }
        Debug.Log(grid_output);
    }

    private IEnumerator PlaceCharactersInWorldCoroutine(string file_path)
    {
        GameData gameData = LoadGameDataFromJson(file_path);
        string gameDataString = JsonConvert.SerializeObject(gameData, Formatting.None);

        string world_Grid_String = GridToString(world_grid_global);
        string character_Grid_String = GridToString(character_Grid);
        string prompt = "Using the information below, place a character (101) on the current grid." +
                        $"Here is the game data with assets and ID. Ignore the format section and just " +
                        $"place the character on the map. Please don't change any other grid indices other " +
                        $"than the indicy on which the player is currently on: {gameDataString}\n" +
                        $"Here is the current world grid: {world_Grid_String}\n";

        // TODO: Add more debug code here

        // Send the prompt to ChatGPT
        Task<ChatResult> chatResultTask = SendPromptToChatGPT(prompt);

        // Wait until the task is completed
        // Lambda
        yield return new WaitUntil(() => chatResultTask.IsCompleted);

        // Process the response
        if (chatResultTask.Status == TaskStatus.RanToCompletion)
        {
            var chatResult = chatResultTask.Result;
            InstantiateCharacterGrid(chatResult.Choices[0].Message.TextContent);

            // After processing initial placements, I can instantiate the grid to reflect these placements
            // PrintGridToDebug(character_Grid);
            // InstantiateGrid(character_Grid, 1);
        }

        // Optionally, after the initial placement, you can start your routine to update character positions
        StartCoroutine(UpdateCharacterPositionsCoroutine(file_path));
    }

    /*
    private string ConstructInitialPlacementPrompt(string filePath)
    {
        // Load the backstory and any other initial game state information needed for the prompt
        string backstory = LoadBackgroundAndEnvironmentTilesFromJson(filePath);

        // Construct the prompt asking for initial character placements
        string prompt = $"{backstory}\n\nPlease place the characters within the world based on the story and game mechanics.";

        return prompt;
    }
    */

    private IEnumerator UpdateCharacterPositionsCoroutine(string filePath)
    {
        while (true)
        {
            // Construct the prompt with the static backstory, current character_Grid, and character IDs
            // I destroy the previous grid here as well
            string prompt = ConstructUpdateCharacterPrompt(filePath);
            Debug.Log("UpdateCharacterPositionsCoroutine Prompt: " + prompt);

            // Send the prompt to ChatGPT
            Task<ChatResult> chatResultTask = SendPromptToChatGPT(prompt);

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
            yield return new WaitUntil(() => chatResultTask.IsCompleted);

            // Process the response
            if (chatResultTask.Status == TaskStatus.RanToCompletion)
            {
                var chatResult = chatResultTask.Result;

                InstantiateCharacterGrid(chatResult.Choices[0].Message.TextContent);
            }

            // InstantiateGrid(character_Grid, 1);
            // Wait for a specified period before updating again
            yield return new WaitForSeconds(5f);
        }
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

    // For updating the characters position in the world
    private string ConstructUpdateCharacterPrompt(string filePath) {
        GameData gameData = LoadGameDataFromJson(filePath);
        string gameDataString = JsonConvert.SerializeObject(gameData, Formatting.None);
        string world_Grid_String = GridToString(world_grid_global);
        string character_Grid_String = GridToString(character_Grid);

        string prompt = "ONLY RESPOND WITH THE 10x10 character grid. Do not add any additional text to your response." +
            "The character grid is below which is currently where the players are on the map. The world map contains information about the map. " +
            "Please see the JSON file and the tiles and characters respective IDs. Please move the character (101) on the grid to somewhere else on the" +
            "map to tile that they can stand on. If he isn't on the grid, place him randomly. The grid you respond to me with should only contain the " +
            "IDs of the players for me to place them on onto the current world grid. Any tile that isn't a player, set to 000." +
                        $"Here is the JSON file. {gameDataString}\n" +
                        $"Here is the current world grid: {world_Grid_String}\n" +
                        $"Here is the character for you to update grid: {character_Grid_String}\n";

        /*
        string prompt = "Please use this JSON file to create me a 10x10 grid based off the id's. Place the environmental tiles and the characters " +
                    "However you'd like. If the world currently has IDs in it, please update it in a way that makes sense." + 
                    $"Here is the JSON file. {gameDataString}\n" +
                    $"Here is the character for you to update grid: {character_Grid_String}\n";
        */

        // Debug.Log("world_Grid_String");
        // PrintGridToDebug(world_grid_global);
        // Debug.Log("character_Grid_String");
        // PrintGridToDebug(character_Grid);
        return prompt;
    }

    private Task<ChatResult> SendPromptToChatGPT(string prompt)
    {
        // Send the prompt to ChatGPT and return the Task object
        // Similar to your existing code for generating the character_Grid
        return api.Chat.CreateChatCompletionAsync(new ChatRequest
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.7,
            MaxTokens = 500,
            Messages = new List<ChatMessage> { new ChatMessage { Role = ChatMessageRole.System, TextContent = prompt } }
        });
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
    void InstantiateGrid(string[,] grid, int y_Pos) {
        // ClearInstantiatedTiles();
        // Debug.LogError("***Instanciating grid***\n\n");
        // PrintGridToDebug(grid);
        for (int i = 0; i < grid.GetLength(0); i++) {
            for (int j = 0; j < grid.GetLength(1); j++) {
                GameObject prefab = GetPrefabById(grid[i, j]);
                if (prefab != null) {
                    // This command makes a copy of the prefab object and adds it to the game world.
                    current_Tile = Instantiate(prefab, new Vector3(i, y_Pos, j), Quaternion.identity);
                    // Groups the tiles together and organizes them neatly
                    current_Tile.transform.parent = environmentTile.transform;
                }
            }
        }
    }

    // For when I recieve a new activity
    private void UpdateCharacterActivity(string character_id, string new_activity) {
        foreach (var character_tile in instantiated_player_tiles) {
            if (character_tile != null && character_tile.name == character_id) {
                var character_data = character_tile.GetComponent<Character>(); 
                if (character_data != null) {
                    character_data.CurrentActivity = new_activity;
                    var text_mesh = character_tile.GetComponentInChildren<TextMeshPro>();
                    if (text_mesh != null) {
                        text_mesh.text = new_activity;
                    }
                }
            }
        }
    }

    private Character GetCharacterDataById(string objectId) {
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
        // ClearInstantiatedTiles();
        // Debug.LogError("***Instanciating grid***\n\n");
        // PrintGridToDebug(grid);

        ClearInstantiatedTiles();

        for (int i = 0; i < grid.GetLength(0); i++)
        {
            for (int j = 0; j < grid.GetLength(1); j++)
            {
                GameObject prefab = GetPrefabById(grid[i, j]);
                if (prefab != null)
                {
                    // This command makes a copy of the prefab object and adds it to the game world.
                    current_Tile_2 = Instantiate(prefab, new Vector3(i, y_Pos, j), Quaternion.identity);
                    // Groups the tiles together and organizes them neatly
                    current_Tile_2.transform.parent = environmentTile.transform;
                    
                    // Update the text element for the activity
                    var character_data = GetCharacterDataById(grid[i, j]);
                    if (character_data != null)
                    {
                        TextMeshPro text_mesh = current_Tile_2.GetComponentInChildren<TextMeshPro>();
                        if (text_mesh != null)
                        {
                            // text_mesh.text = character_data.CurrentActivity;
                            text_mesh.text = character_data.CurrentActivity;
                        }
                    }

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

    // Archived
    /*
 
    // Not using currently
    private string LoadBackgroundAndEnvironmentTilesFromJson(string filePath)
    {
        // Read the entire JSON content from the file
        string jsonContent = File.ReadAllText(filePath);
        // Deserialize the JSON content into the GameData object
        GameData gameData = JsonConvert.DeserializeObject<GameData>(jsonContent);

        // Extract the background story from the GameData object
        string backgroundStory = gameData.BackgroundStory;

        // Initialize a StringBuilder to construct the description string for environmental tiles
        StringBuilder environmentTilesDescription = new StringBuilder();
        environmentTilesDescription.AppendLine("Environmental Tiles:");

        // Iterate over each EnvironmentTile object in the GameData object to append its description to the StringBuilder
        foreach (var tile in gameData.EnvironmentTiles)
        {
            environmentTilesDescription.AppendLine($"ObjectID: {tile.ObjectID}, Model: {tile.EnvironmentModelName}, Type: {tile.Type}");
        }

        // Combine the background story and the environmental tiles description into one string
        string combinedInfo = 
            "Only respond with the grid. Grid instructions: " +
            gridGenerationInstructions +
            backgroundStory + 
            "\n\n" + 
            environmentTilesDescription.ToString();

        // Return the combined string
        return combinedInfo;
    }
       // Interface for coroutines
    // Used for pausing methods
    private IEnumerator GenerateGridWithChatGPTCoroutine(string filePath)
    {
        while (true)
        {
            if (File.Exists(filePath))
            {
                GameData gameData = LoadGameDataFromJson(filePath);
                string gameDataString = JsonConvert.SerializeObject(gameData, Formatting.None);
                string prompt = $"Here is the game data with assets and IDs: {gameDataString}\n" +
                                "Based on the above information, generate a 10x10 character_Grid world.";

                Task<ChatResult> chatResultTask = api.Chat.CreateChatCompletionAsync(new ChatRequest
                {
                    Model = Model.ChatGPTTurbo,
                    Temperature = 0.7,
                    MaxTokens = 500,
                    Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = ChatMessageRole.System, TextContent = prompt }
                }
                });

                // Wait until the task is completed (Lambda)
                yield return new WaitUntil(() => chatResultTask.IsCompleted);

                if (chatResultTask.Status == TaskStatus.RanToCompletion)
                {
                    var chatResult = chatResultTask.Result;
                    if (chatResult != null && chatResult.Choices != null && chatResult.Choices.Count > 0)
                    {
                        string responseText = chatResult.Choices[0].Message.TextContent;
                        Debug.LogError("inside: chatResult != null && chatResult.Choices != null && chatResult.Choices.Count");
                        ProcessChatGPTResponseInstanciateGrid(responseText);
                    }
                    else
                    {
                        Debug.LogError("ChatResult is null");
                    }
                }
                // Wait for 10 seconds before calling again
                yield return new WaitForSeconds(10f);
            }
            else
            {
                Debug.LogError("Cannot find the JSON file: " + filePath);
                // File not found. wait before retrying
                yield return new WaitForSeconds(10f);
            }
        }
    }

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

public class OpenAIController : MonoBehaviour
{
    public TMP_Text textField;
    public TMP_InputField inputField;
    public Button okButton;

    private OpenAIAPI api;
    private List<ChatMessage> messages;

    public bool isDebug = true;

    // Create a 10x10 character_Grid to hold the IDs (2D array)
    // [SerializeField]
    string[,] character_Grid = new string[10, 10];

    void Start()
    {
        api = new OpenAIAPI(Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User));
        // Find the JSON file (Application.dataPath points to the location where the game's data is stored)
        // Creates a file path to the JSON. Fore example: asset\background_Settings.json
        string filePath = Path.Combine(Application.dataPath, "background_Settings.json");
        // GenerateWorldWithChatGPT(filePath);
        StartCoroutine(GenerateWorldWithChatGPTCoroutine(filePath));
    }

    public class GameData
    {
        public FormatSpecification Format { get; set; }
        public string GameTypeAndMechanics { get; set; }
        public string BackgroundStory { get; set; }
        public List<EnvironmentTile> EnvironmentTiles { get; set; }
        public List<Character> Characters { get; set; }
    }

    public class FormatSpecification
    {
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
    }

    public class Character
    {
        public string ObjectID { get; set; }
        public string CharacterModelName { get; set; }
        public string CharacterDescription { get; set; }
        public string Type { get; set; }
    }

    private GameData LoadGameDataFromJson(string filePath)
    {
        string jsonContent = File.ReadAllText(filePath);
        // Convert string to JSON object
        var gameData = JsonConvert.DeserializeObject<GameData>(jsonContent);
        return gameData;
    }

    private async void GenerateWorldWithChatGPT(string filePath)
    {
        if (File.Exists(filePath))
        {
            GameData gameData = LoadGameDataFromJson(filePath);
            string gameDataString = JsonConvert.SerializeObject(gameData, Formatting.None);

            string prompt = $"Here is the game data with assets and IDs: {gameDataString}\n" +
                            "Based on the above information, generate a 10x10 character_Grid world.";

            var chatResult = await api.Chat.CreateChatCompletionAsync(new ChatRequest
            {
                Model = Model.ChatGPTTurbo,
                // NOTE: Temperature is level of creativity of ChatGPT. Higher is more varied
                // but creative, and lower is less creative but more predictable
                Temperature = 0.7,
                MaxTokens = 500,
                // The conversation history
                Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.System, TextContent = prompt }
            }
            });

            // Process the response from ChatGPT
            if (chatResult != null && chatResult.Choices != null && chatResult.Choices.Count > 0)
            {
                string responseText = chatResult.Choices[0].Message.TextContent;
                ProcessChatGPTResponse(responseText);
            }
            else
            {
                Debug.LogError("ChatResult is null or does not contain choices.");
            }
        }
        else
        {
            Debug.LogError("Cannot find the JSON file: " + filePath + "\n\n");
        }
    }

    // Interface for coroutines
    // Used for pausing methods
    private IEnumerator GenerateWorldWithChatGPTCoroutine(string filePath)
    {
        while (true)
        {
            if (File.Exists(filePath))
            {
                GameData gameData = LoadGameDataFromJson(filePath);
                string gameDataString = JsonConvert.SerializeObject(gameData, Formatting.None);
                string prompt = $"Here is the game data with assets and IDs: {gameDataString}\n" +
                                "Based on the above information, generate a 10x10 character_Grid world.";

                Task<ChatResult> chatResultTask = api.Chat.CreateChatCompletionAsync(new ChatRequest
                {
                    Model = Model.ChatGPTTurbo,
                    Temperature = 0.7,
                    MaxTokens = 500,
                    Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = ChatMessageRole.System, TextContent = prompt }
                }
                });

                // Wait until the task is completed (Lambda)
                yield return new WaitUntil(() => chatResultTask.IsCompleted);

                if (chatResultTask.Status == TaskStatus.RanToCompletion)
                {
                    var chatResult = chatResultTask.Result;
                    if (chatResult != null && chatResult.Choices != null && chatResult.Choices.Count > 0)
                    {
                        string responseText = chatResult.Choices[0].Message.TextContent;
                        Debug.LogError("inside: chatResult != null && chatResult.Choices != null && chatResult.Choices.Count");
                        ProcessChatGPTResponse(responseText);
                    }
                    else
                    {
                        Debug.LogError("ChatResult is null");
                    }
                }
                // Wait for 10 seconds before calling again
                yield return new WaitForSeconds(10f);
            }
            else
            {
                Debug.LogError("Cannot find the JSON file: " + filePath);
                // File not found. wait before retrying
                yield return new WaitForSeconds(10f);
            }
        }
    }

    void ProcessChatGPTResponse(string responseText)
    {
        // Remove the colon that keeps showing up (Bug)
        if (responseText.EndsWith(":"))
        {
            responseText = responseText.Substring(0, responseText.Length - 1);
        }

        // 1) responseText.Trim() - removes any extra spaces or whitespace from the beginning and end of responseText
        // 2) .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries) - cuts the cleaned-up text into separate
        // pieces (or lines) every time it finds a newline character ('\n') or when 'Enter' is pressed
        string[] lines = responseText.Trim().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            // Debug.Log("***Trimmed lines from ChatGPT Response***\n\n" + lines[i] + "\n\n");
        }

        // Check that we have 10 lines to match our expected character_Grid size
        if (lines.Length == 10)
        {
            // Parse each line
            for (int i = 0; i < lines.Length; i++)
            {
                // Split the line into cells by the pipe character
                string[] cells = lines[i].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                // Check that we have 10 cells in each line
                if (cells.Length == 10)
                {
                    for (int j = 0; j < cells.Length; j++)
                    {
                        // Trim the cell to get the ID and assign it to the character_Grid
                        string cellId = cells[j].Trim();
                        character_Grid[i, j] = cellId;
                    }
                }
                else
                {
                    Debug.LogError("Unexpected number of cells in line: " + lines[i]);
                    // Exit if format is incorrect
                    return;
                }
            }

            PrintGridToDebug(character_Grid);
            // Now I have a character_Grid with IDs, you can instantiate game objects or tiles based on these IDs
            InstantiateGrid(character_Grid);
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

    // Converts a 2D array to a string
    void PrintGridToDebug(string[,] character_Grid)
    {
        string gridOutput = "";

        // GetLength(0) returns the size of the first dimension
        for (int i = 0; i < character_Grid.GetLength(0); i++)
        {
            // GetLength(1) returns the size of the second dimension
            for (int j = 0; j < character_Grid.GetLength(1); j++)
            {
                // Append each cell's content to the string
                gridOutput += character_Grid[i, j] + " ";
            }
            // Newline at the end of each row
            gridOutput += "\n"; 
        }

        Debug.LogError("***character_Grid output***\n\n" + gridOutput + "\n\n");
    }

    public GameObject grassPrefab;
    public GameObject waterPrefab;
    public GameObject rockPrefab;
    public GameObject civilianMan;
    public GameObject marketPrefab;
    public GameObject environmentTile;
    public GameObject GetPrefabById(string id)
    {
        switch (id.ToLower())
        {
            case "001": return grassPrefab;
            case "002": return waterPrefab;
            case "003": return rockPrefab;
            case "004": return marketPrefab;
            case "101": return civilianMan;
            // Duplicated for now
            case "102": return civilianMan;
            default: return null;
        }
    }

    GameObject current_Tile;
    void InstantiateGrid(string[,] character_Grid)
    {
        Debug.LogError("***Instanciating the character_Grid***\n\n");
        for (int i = 0; i < character_Grid.GetLength(0); i++)
        {
            for (int j = 0; j < character_Grid.GetLength(1); j++)
            {
                GameObject prefab = GetPrefabById(character_Grid[i, j]);
                if (prefab != null)
                {
                    current_Tile = Instantiate(prefab, new Vector3(i, 0, j), Quaternion.identity);
                    current_Tile.transform.parent = environmentTile.transform;
                }
            }
        }
    }

    private void StartConversation()
    {
        // Test message. Please reply 'yes' that you got it.
        messages = new List<ChatMessage> { 
            new ChatMessage(ChatMessageRole.System, "") 
        };

        inputField.text = "";
        string startString = "This is ChatGPT! Ask me anything!";
        textField.text = startString;
        Debug.Log(startString);
    }

    private async void GetResponse()
    {
        if (inputField.text.Length < 1)
        {
            return;
        }
        // Disable the OK button
        okButton.enabled = false;

        // Fill the user message from the input field
        ChatMessage userMessage = new ChatMessage();
        userMessage.Role = ChatMessageRole.User;
        userMessage.TextContent = inputField.text;
        if (userMessage.TextContent.Length > 100)
        {
            // Limit messages to 100 characters
            userMessage.TextContent = userMessage.TextContent.Substring(0, 100);
        }

        Debug.Log(string.Format("{0}: {1}", userMessage.Role, userMessage.TextContent));

        // Add the message to the list
        messages.Add(userMessage);

        // Update the text field with the user message
        textField.text = string.Format("You: {0}", userMessage.TextContent);

        // Clear the input field
        inputField.text = "";

        // Send the entire chat to OpenAI to get the next message
        var chatResult = await api.Chat.CreateChatCompletionAsync(new OpenAI_API.Chat.ChatRequest()
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.1,
            MaxTokens = 150,
            Messages = messages
        });

        // Get the response message
        ChatMessage responseMessage = new ChatMessage();
        responseMessage.Role = chatResult.Choices[0].Message.Role;
        responseMessage.TextContent = chatResult.Choices[0].Message.TextContent;
        Debug.Log(string.Format("{0}: {1}", responseMessage.rawRole, responseMessage.TextContent));

        // Add the rresponse to the list of messages
        messages.Add(responseMessage);

        // Update the text field with the response
        textField.text = string.Format("You: {0}\n\nChatGPT: {1}", userMessage.TextContent, responseMessage.TextContent);

        okButton.enabled = true;
    }

    private IList<ChatMessage> LoadMessagesFromJson(string filePath)
    {
        string jsonContent = File.ReadAllText(filePath);
        var chatRequest = JsonConvert.DeserializeObject<ChatRequest>(jsonContent);
        return chatRequest.Messages;
    }

    private string ConvertResponseToJson(ChatResult chatResult)
    {
        return JsonConvert.SerializeObject(chatResult, Formatting.Indented);
    }

    private async void SendJsonAndGetResponse(string jsonFilePath)
    {
        okButton.enabled = false;

        // Load messages from the JSON file
        var messages = LoadMessagesFromJson(jsonFilePath);

        // Send the messages to ChatGPT
        var chatResult = await api.Chat.CreateChatCompletionAsync(new ChatRequest
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.1,
            MaxTokens = 150,
            Messages = messages
        });

        // Convert the response back to JSON
        string jsonResponse = ConvertResponseToJson(chatResult);

        Debug.Log(jsonResponse);

        okButton.enabled = true;
    }
    */

    // Update is called once per frame
    // Be careful with putting any communication with openAI in the update function.
    // If the communication is bound to your frames, you could accidently send a 
    // ton of communication requests a second (60+ frames) because it's bound to
    // your games frames
    /*
    void Update()
    {
        
    }
    */
}

/*BACKUP
    public TMP_Text textField;
    public TMP_InputField inputField;
    public Button okButton;

    private OpenAIAPI api;
    private List<ChatMessage> messages;

    public bool isDebug = true;

    void Start()
    {
        api = new OpenAIAPI(Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User));
        // Find the JSON file (Application.dataPath points to the location where the game's data is stored)
        // Creates a file path to the JSON. Fore example: asset\background_Settings.json
        string filePath = Path.Combine(Application.dataPath, "background_Settings.json");
        GenerateWorldWithChatGPT(filePath);
    }

    public class GameData
    {
        public FormatSpecification Format { get; set; }
        public string GameTypeAndMechanics { get; set; }
        public string BackgroundStory { get; set; }
        public List<EnvironmentTile> EnvironmentTiles { get; set; }
        public List<Character> Characters { get; set; }
    }

    public class FormatSpecification
    {
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
    }

    public class Character
    {
        public string ObjectID { get; set; }
        public string CharacterModelName { get; set; }
        public string CharacterDescription { get; set; }
        public string Type { get; set; }
    }

    private GameData LoadGameDataFromJson(string filePath)
    {
        // Read all the text from specified filepath
        string jsonContent = File.ReadAllText(filePath);
        if (isDebug)
        {
            // Debug.Log("***The JSON file contents as a string***\n\n" + jsonContent + "\n\n");
        }
        // DeserializeObject is a method from Newtonsoft.Json library.
        // This function converts the JSON string into a gameData object 
        // that I've specified above.
        var gameData = JsonConvert.DeserializeObject<GameData>(jsonContent);
        return gameData;
    }

    private async void GenerateWorldWithChatGPT(string filePath)
    {
        if (isDebug)
        {
            // Debug.Log("***Looking for JSON file at***\n\n" + filePath + "\n\n");
        }
        if (File.Exists(filePath))
        {
            // Load game data from JSON file. This makes it easy to manipulate
            // my data in Unity becaues it's in object oriented format
            GameData gameData = LoadGameDataFromJson(filePath);
            // Re-serialize the necessary parts of gameData back to a string
            string gameDataString = JsonConvert.SerializeObject(gameData, Formatting.None);

            // Prepare the prompt for ChatGPT
            string prompt = $"Here is the game data with assets and IDs: {gameDataString}\n" +
                            "Based on the above information, generate a 10x10 character_Grid world.";

            if (isDebug)
            {
                // Debug.Log("***The serialized prompt being sent to ChatGPT***\n\n" + prompt + "\n\n");
            }

            // Send the prompt to ChatGPT
            var chatResult = await api.Chat.CreateChatCompletionAsync(new ChatRequest
            {
                Model = Model.ChatGPTTurbo,
                // NOTE: Temperature is level of creativity of ChatGPT. Higher is more varied
                // but creative, and lower is less creative but more predictable
                Temperature = 0.7,
                MaxTokens = 500,
                // The conversation history
                Messages = new List<ChatMessage>
            {
                // NOTE: System messages are used to set the behavior of the assistant.
                // This could involve providing context or instructions that
                // influence how the AI model generates its responses.
                // System messages are not part of the conversational flow between
                // the user and the assistant but are instead used to control
                // or guide the interaction.
                new ChatMessage { Role = ChatMessageRole.System, TextContent = prompt }
            }
            });
            if (isDebug) { 
                // Debug.Log("***ChatGPT Response***\n\n" + chatResult + "\n\n"); 
            }
            
            // Process the response from ChatGPT
            if (chatResult != null && chatResult.Choices != null && chatResult.Choices.Count > 0)
            {
                string responseText = chatResult.Choices[0].Message.TextContent;
                if (isDebug)
                {
                    // Debug.Log("ChatGPT Response (chatResult.Choices[0].Message.TextContent;)\n\n" + responseText + "\n\n");
                }
                // Pass the string, not the ChatResult object
                ProcessChatGPTResponse(responseText);
            }
            else
            {
                // Debug.LogError("ChatResult is null or does not contain choices.");
            }
        }
        else
        {
            Debug.LogError("Cannot find the JSON file: " + filePath + "\n\n");
        }
    }

    // This method assumes responseText is the string containing the ChatGPT response as shown above.
    void ProcessChatGPTResponse(string responseText)
    {
        // Remove the colon that keeps showing up (Bug)
        if (responseText.EndsWith(":"))
        {
            responseText = responseText.Substring(0, responseText.Length - 1);
        }

        // 1) responseText.Trim() - removes any extra spaces or whitespace from the beginning and end of responseText
        // 2) .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries) - cuts the cleaned-up text into separate
        // pieces (or lines) every time it finds a newline character ('\n') or when 'Enter' is pressed
        string[] lines = responseText.Trim().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            // Debug.Log("***Trimmed lines from ChatGPT Response***\n\n" + lines[i] + "\n\n");
        }

        // Create a 10x10 character_Grid to hold the IDs (2D array)
        // [SerializeField]
        string[,] character_Grid = new string[10, 10];

        // Check that we have 10 lines to match our expected character_Grid size
        if (lines.Length == 10)
        {
            // Parse each line
            for (int i = 0; i < lines.Length; i++)
            {
                // Split the line into cells by the pipe character
                string[] cells = lines[i].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                // Check that we have 10 cells in each line
                if (cells.Length == 10)
                {
                    for (int j = 0; j < cells.Length; j++)
                    {
                        // Trim the cell to get the ID and assign it to the character_Grid
                        string cellId = cells[j].Trim();
                        character_Grid[i, j] = cellId;
                    }
                }
                else
                {
                    Debug.LogError("Unexpected number of cells in line: " + lines[i]);
                    // Exit if format is incorrect
                    return;
                }
            }

            PrintGridToDebug(character_Grid);
            // Now I have a character_Grid with IDs, you can instantiate game objects or tiles based on these IDs
            InstantiateGrid(character_Grid);
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

    // Converts a 2D array to a string
    void PrintGridToDebug(string[,] character_Grid)
    {
        string gridOutput = "";

        // GetLength(0) returns the size of the first dimension
        for (int i = 0; i < character_Grid.GetLength(0); i++)
        {
            // GetLength(1) returns the size of the second dimension
            for (int j = 0; j < character_Grid.GetLength(1); j++)
            {
                // Append each cell's content to the string
                gridOutput += character_Grid[i, j] + " ";
            }
            // Newline at the end of each row
            gridOutput += "\n"; 
        }

        Debug.LogError("***character_Grid output***\n\n" + gridOutput + "\n\n");
    }
    // Interface for coroutines
    // Used for pausing methods



    public GameObject grassPrefab;
    public GameObject waterPrefab;
    public GameObject rockPrefab;
    public GameObject civilianMan;
    public GameObject marketPrefab;
    public GameObject GetPrefabById(string id)
    {
        switch (id.ToLower())
        {
            case "001": return grassPrefab;
            case "002": return waterPrefab;
            case "003": return rockPrefab;
            case "004": return marketPrefab;
            case "101": return civilianMan;
            // Duplicated for now
            case "102": return civilianMan;
            default: return null;
        }
    }
    void InstantiateGrid(string[,] character_Grid)
    {
        Debug.LogError("***Instanciating the character_Grid***\n\n");
        for (int i = 0; i < character_Grid.GetLength(0); i++)
        {
            for (int j = 0; j < character_Grid.GetLength(1); j++)
            {
                GameObject prefab = GetPrefabById(character_Grid[i, j]);
                if (prefab != null)
                {
                    Instantiate(prefab, new Vector3(i, 0, j), Quaternion.identity);
                }
            }
        }
    }
*/