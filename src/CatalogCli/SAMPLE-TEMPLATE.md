# Sample Catalog Template

Use this as a template for creating new catalog entries in Excel.

## roles.tsv
```
Id	Name	Description	CopilotMultiplier
architect	Solution Architect	Technical architecture and system design	0.8
dev	Developer	Full-stack developer with modern tooling	0.6
devops	DevOps engineer	Infrastructure and deployment automation specialist	0.7
em	Engagement manager	Manages client relationships and project planning	1.0
qa	QA Engineer	Quality assurance and test automation	0.65
security	Security Engineer	Security assessment and implementation	0.85
ux	UX Designer	User experience and interface design	0.9
```

## entries.tsv
```
Id	Name	Description	Category	TechStack	Tags	architect	dev	devops	em	qa	security	ux
salesforce-apex-class	Apex Class Development	Custom Apex class with unit tests	feature	salesforce	salesforce;apex;backend	4	16	0	2	6	2	0
salesforce-lwc	Lightning Web Component	Custom LWC with reactive properties	feature	salesforce	salesforce;lwc;frontend;javascript	0	28	0	0	4	0	8
blazor-data-grid	Advanced Data Grid	Filterable, sortable data grid with pagination	feature	blazor-azure	frontend;data;ui-component	0	32	0	0.5	0	0	8
blazor-auth	Blazor Authentication	OAuth2/OIDC with Azure AD integration	feature	blazor-azure	authentication;security;blazor	2	24	0	1	0	6	0
nodejs-api	Node.js REST API	Express-based REST API with OpenAPI docs	backend	nodejs	api;rest;backend;nodejs	4	40	0	1	0	2	0
react-component	React UI Component	Reusable React component with Storybook	frontend	react-aws	frontend;react;ui-component	0	20	0	0.5	4	0	12
shared-crud	Generic CRUD Feature	Create, read, update, delete operations	feature	shared	crud;data;forms	0	20	0	0.5	0	0	0
shared-auth	User Authentication	OAuth2/OIDC authentication with SSO support	feature	shared	authentication;security;backend	0	30	0	1	0	8	0
devops-cicd	CI/CD Pipeline	Automated build and deployment pipeline	devops	shared	devops;ci;cd;automation	2	0	28	0.5	0	0	0
devops-terraform	Infrastructure as Code	Terraform templates for cloud resources	devops	blazor-azure	iac;terraform;azure;cloud	4	0	40	0.5	0	0	0
```

## Notes

**Copy these into Excel:**
1. Create new Excel workbook
2. Paste roles data into Sheet1, entries data into Sheet2
3. Save each sheet as Tab Delimited Text (.tsv)
4. Edit as needed
5. Import using CLI tool

**Column Rules:**
- **Id**: No spaces, use hyphens (e.g., `my-feature-id`)
- **TechStack**: Use standard values or leave empty
- **Tags**: Semicolon-separated (e.g., `frontend;data;api`)
- **Hours**: Decimal numbers for Medium (M) size, empty if role not involved

**Tech Stacks:**
- `salesforce` - Salesforce platform features
- `blazor-azure` - Blazor + Azure stack
- `dotnet` - .NET/ASP.NET Core
- `nodejs` - Node.js ecosystem
- `react-aws` - React + AWS
- `shared` - Cross-platform features
