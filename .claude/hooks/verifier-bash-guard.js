// PreToolUse hook (DEV-04): inside the verifier agent, allows only commands that read
// the repository or run the checks. Exit code 2 blocks the command. Other agents pass.
// The command text is checked with its quotes removed, and text the shell would expand is blocked.
const allowed = [
  /^cd [\w:/.-]+$/,
  /^git (status|diff|log|show|rev-parse|rev-list|ls-files|blame|check-ignore|cat-file|merge-base)( |$)/,
  /^dotnet build -c Release$/,
  /^dotnet test -c Release --no-build$/,
  /^dotnet format --verify-no-changes$/,
  /^(cat|head|tail|grep|wc|ls|echo) /,
];

function reason(command) {
  // A redirection is harmless only if its word ends there. Removing quotes comes second,
  // because a quote attached to the redirection is part of its target.
  const text = command.replace(/ 2>(&1|\/dev\/null)(?=[\s;&|]|$)/g, '').replace(/["']/g, '');

  if (/[<>`\\{}]|\$\S/.test(text)) {
    return 'redirection, expansion, substitution and backslashes are not allowed';
  }
  // Prefixes, because git accepts a unique prefix of a long option.
  if (/--out|--ext|--textc|--exec/.test(text)) {
    return 'options that write files or run programs are not allowed';
  }

  const segments = text.split(/&&|\|\||[;|&\n]/).map(s => s.trim());
  const blocked = segments.find(s => !allowed.some(pattern => pattern.test(s)));
  return blocked === undefined ? null : `not an allowed read-only command: ${blocked}`;
}

let input = '';
process.stdin.on('data', chunk => (input += chunk));
process.stdin.on('end', () => {
  const { agent_type: agentType, tool_input: toolInput } = JSON.parse(input);
  const problem = agentType === 'verifier' ? reason(toolInput.command) : null;
  if (problem) {
    console.error(`The verifier cannot run this: ${problem}.`);
    process.exit(2);
  }
});
