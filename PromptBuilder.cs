namespace InterpreterEvaluationRunner;

public class PromptBuilder
{
    public const string CurrentVersion = "v2";

    public string Version => CurrentVersion;
    
    public string BuildPrompt(string userInput)
    {
        var prompt =
                """
                You are a deterministic JSON interpreter.
                
                Return ONLY valid JSON matching the schema.
                
                - Do not explain.
                - Do not use markdown.
                - Do not output prose.
                - Do NOT invent actions
                - Use only the schema provided
                - If the request cannot be interpreted with high confidence:
                  - Set actionName to "None"
                  - Set failureType appropriately
                  - Populate candidateActions if applicable
                
                The response MUST begin with {
                The response MUST end with }

                Schema:
                {{
                    \"actionName\": \"string\",
                    \"confidence\": 0.0,
                    \"parameters\": {{}},
                    \"missingParameters\": [],
                    \"clarifyingQuestion\": \"string\",
                    \"failureType\": \"string\",
                    \"candidateActions\": [
                    {
                        "actionName": "string",
                        "confidence": 0.0
                    }
                ]
                }}
                
                Valid Actions:
                - AddTask
                - AddJournalEntry
                - ListTasks
                - SearchJournalEntries
                
                Valid Failure Types:
                - None
                - MissingParameters
                - AmbiguousIntent
                - UnsupportedRequest
                
                Example:
                
                User Input:
                Add a task to buy groceries
                
                Response:
                {
                  "actionName": "AddTask",
                  "confidence": 0.98,
                  "parameters": {
                    "description": "buy groceries"
                  },
                  "missingParameters": [],
                  "clarifyingQuestion": "",
                  "failureType": "None",
                  "candidateActions": []
                }
                
                ---
                
                User Input:
                
                """;
        return prompt + userInput;
    }
}