const cwd = String.raw`D:\Projects\AI\ThingsBooksy-BE`;
const R = String.raw;
const T = R`D:\Projects\AI\ThingsBooksy-BE\backend\src\Modules\Resources\ThingsBooksy.Modules.Resources.IntegrationTests`;
module.exports = [
 ["read docs (allowed root)",        {cwd, tool_name:"Read", tool_input:{file_path: R`D:\Projects\AI\ThingsBooksy-BE\docs\agent-fleet-v4\decisions.md`}}, 0],
 ["read test project (allowed)",     {cwd, tool_name:"Read", tool_input:{file_path: T + R`\IntegrationTestCollection.cs`}}, 0],
 ["read Core (denied)",              {cwd, tool_name:"Read", tool_input:{file_path: R`D:\Projects\AI\ThingsBooksy-BE\backend\src\Modules\Resources\ThingsBooksy.Modules.Resources.Core\Module.cs`}}, 2],
 ["read UPPERCASE fwd (denied)",     {cwd, tool_name:"Read", tool_input:{file_path: "D:/Projects/AI/ThingsBooksy-BE/BACKEND/SRC/Shared/x.cs"}}, 2],
 ["read UPPERCASE allowed root",     {cwd, tool_name:"Read", tool_input:{file_path: "D:/PROJECTS/AI/THINGSBOOKSY-BE/DOCS/agent-fleet-v4/decisions.md"}}, 0],
 ["read relative denied",            {cwd, tool_name:"Read", tool_input:{file_path: "backend/src/Shared/x.cs"}}, 2],
 ["read dotdot escape (denied)",     {cwd, tool_name:"Read", tool_input:{file_path: "docs/../backend/src/Shared/x.cs"}}, 2],
 ["read \\?\ prefix (denied)",    {cwd, tool_name:"Read", tool_input:{file_path: R`\?\D:\Projects\AI\ThingsBooksy-BE\backend\src\Shared\x.cs`}}, 2],
 ["read 8.3 short name (denied)",    {cwd, tool_name:"Read", tool_input:{file_path: R`D:\Projects\AI\THINGS~1\backend\src\Shared\x.cs`}}, 2],
 ["read CLAUDE.md root (denied)",    {cwd, tool_name:"Read", tool_input:{file_path: R`D:\Projects\AI\ThingsBooksy-BE\CLAUDE.md`}}, 2],
 ["grep with allowed path",          {cwd, tool_name:"Grep", tool_input:{pattern:"class", path: T}}, 0],
 ["grep with Core path (denied)",    {cwd, tool_name:"Grep", tool_input:{pattern:"class", path: R`D:\Projects\AI\ThingsBooksy-BE\backend\src\Modules`}}, 2],
 ["grep NO path (denied)",           {cwd, tool_name:"Grep", tool_input:{pattern:"class"}}, 2],
 ["glob **/*.csproj no path (denied)",{cwd, tool_name:"Glob", tool_input:{pattern:"**/*.csproj"}}, 2],
 ["glob with allowed path",          {cwd, tool_name:"Glob", tool_input:{pattern:"**/*.cs", path: T}}, 0],
 ["glob absolute pattern denied",    {cwd, tool_name:"Glob", tool_input:{pattern:"D:/Projects/AI/ThingsBooksy-BE/backend/src/**/*.cs"}}, 2],
 ["garbage input (denied)",          "not json", 2],
 ["other tool passes through",       {cwd, tool_name:"Write", tool_input:{file_path:"x"}}, 0],
];
