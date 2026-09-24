// Prototype: dump raw hook stdin to a log file, never block.
const fs = require('fs');
const path = require('path');
let raw = '';
try { raw = fs.readFileSync(0, 'utf8'); } catch (e) { raw = 'READ-ERROR ' + e; }
fs.appendFileSync(path.join(__dirname, 'proto-posttooluse.log'), JSON.stringify({ at: new Date().toISOString(), raw }) + '\n');
process.exit(0);
