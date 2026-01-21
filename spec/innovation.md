# Innovation Day Ideas

## Prompting for AI (instructions file)

1. Prompts need to include things that the AI should ALWAYS consider (like security, performance, accessibility, etc) (Shalimar working on)

## MCP Enhancements

1. Need multiple catalogs for different business units (Allen completed)
    - Recommend Single Catalog with Category/Tags.
        - "category": "salesforce-feature",
        - "tags": ["salesforce", "apex", "backend"],
        - "category": "blazor-feature",
        - "tags": ["blazor", "frontend", "dotnet"],
2. Need cross-cutting roles for a project that are outside the catalog - these roles aren't based on features, they are time-based for the duration of the project (like project manager, business analyst, etc)
    - A cross-cutting role might be fractional (like 0.2 FTE for a 3 month project)
    - Should there be a step where the total estimate is converted to number of weeks?
    - How should we determine resources available, to accurately determine the total number of weeks to deliver?
3. Roles should have seniority levels (like junior, mid-level, senior) that impact the time necessary - like a multiplier based on the level of the resource
    - base level role should be "mid-level"
    - add columns for multipliers for junior and senior
4. Update [instructions.md](/src/estimator-mcp/data/instructions.md) file with specific procedures the AI should follow
5. Roles per techstack
    - Different roles for different techstacks (i.e. Salesforce Architect vs .NET Architect)
    - Need to know which roles apply to which techstacks
6. Switch from json file to database to store catalog and roles
    - Sqlite or PostgreSQL for lightweight options
    - Consider using EF Core for data access

## Deployment

1. Convert to http-based MCP service model
    - Use aspnetcore middleware to require authentication
    - Use API key in HTTP header for authentication
    - Host on Azure App Service
2. Enhance catalog management web UI
    - Require authentication
    - Possibly merge Blazor app into MCP server project (or visa versa) so both can be deployed on the same Azure App Service

## Next steps to consider

1. Add/use rate cards
2. Roles based on location with cost considerations (i.e. offshore team member delivering on the project)
3. Use rates/costs to generate project budget estimates
