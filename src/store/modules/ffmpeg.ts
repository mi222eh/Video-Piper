import { execFile } from 'child_process';
import * as path from 'path';
import {CommandArguments, CurrentWorkingDirectory, FinishedListener, CommandInputList, CommandOutput, CommandInput} from './types/ExecutionTypes';

const getPath = () => path.join(__statics, 'bin', 'ffmpeg.exe');



interface ExecuteOptions extends FinishedListener, CurrentWorkingDirectory, CommandArguments{}

function execute({ cwd, args, finishedListener }: ExecuteOptions) {
    console.log({ cwd, args });
    return execFile(
        getPath(),
        [...args],
        {
            cwd: cwd,
            maxBuffer: 1024 * 1024 * 10
        },
        finishedListener
    );
}
interface CombineOptions extends CommandInputList, CommandOutput, CurrentWorkingDirectory, FinishedListener {}
function combine({
    inputs = [],
    output = '',
    cwd = '',
    finishedListener = () => ''
}: CombineOptions) {
    if (inputs.length < 2) {
        throw new Error('at least two inputs required');
    }
    if (typeof output !== 'string') {
        throw new Error('output required');
    }
    if (typeof cwd !== 'string') {
        throw new Error('cwd required');
    }
    const [input1, input2] = inputs;
    const args = ['-i', input1, '-i', input2, '-shortest', `${output}`];
    return execute({ cwd, args, finishedListener });
}

interface ConvertOptions extends CommandInput, CommandOutput, CurrentWorkingDirectory, FinishedListener {}

function convert({
    input = '',
    output = '',
    cwd = '',
    finishedListener = () => ''
}:ConvertOptions) {
    if (typeof input !== 'string') {
        throw new Error('input required');
    }
    if (typeof output !== 'string') {
        throw new Error('output required');
    }
    if (typeof cwd !== 'string') {
        throw new Error('cwd required');
    }
    let convertArgs: string[] = [];
    switch (path.extname(output)) {
        case 'mp3':
            convertArgs = ['-vn', '-ab', '256'];
            break;
        case 'mp4':
            convertArgs = ['-c:v', 'libx264'];
            break;
        default:
            break;
    }
    const args = ['-i', input, ...convertArgs, `${output}`];
    return execute({ cwd, args, finishedListener });
}

export default {
    combine,
    convert
};
