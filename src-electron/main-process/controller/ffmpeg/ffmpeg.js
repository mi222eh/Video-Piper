import { execFile } from 'child_process';
import terminate from 'terminate';
import path from 'path';

const ffmpeg = path.join(__statics, 'bin', 'ffmpeg.exe');

let cancel = () => {};

function execute ({ cwd, args }) {
	console.log({ cwd, args });
    return new Promise((resolve, reject) => {
        const cp = execFile(ffmpeg, [...args], {
            cwd: cwd,
            shell: true,
            maxBuffer: 1024 * 1024 * 10
        }, (err, out) => {
			console.log({ err, out });
            if (err) {
                reject(err);
            } else {
                resolve(out);
            }
        });
        cancel = ({ message }) => {
            terminate(cp.pid);
            reject(message);
        };
    });
}

function combine ({ inputs = [], output = '', cwd = '' }) {
    if (inputs.length < 2) {
        return Promise.reject('at least two inputs required');
    }
    if (typeof output !== 'string') {
        return Promise.reject('output required');
    }
    if (typeof cwd !== 'string') {
        return Promise.reject('cwd required');
    }
    const [input1, input2] = inputs;
    let args = ['-i', input1, '-i', input2, '-shortest', `"${output}"`];
    return execute({ cwd, args });
}

function convert ({ input = '', output = '', cwd = '' }) {
    if (typeof input !== 'string') {
        return Promise.reject('input required');
    }
    if (typeof output !== 'string') {
        return Promise.reject('output required');
    }
    if (typeof cwd !== 'string') {
        return Promise.reject('cwd required');
    }
    let args = ['-i', input, `"${output}"`];
    return execute({ cwd, args });
}

function abort () {
    cancel({
        message: 'Cancelled'
    });
}

export default {
    combine,
    convert,
    abort
};
