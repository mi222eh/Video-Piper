import { execFile } from 'child_process';
import terminate from 'terminate';
import { join } from 'path';

const youtubedl = join(__statics, 'bin', 'youtube-dl.exe');

let cancel = ({ message }) => {

};

function execute ({ cwd, url, args }) {
	console.log({ cwd, url, args });

    return new Promise((resolve, reject) => {
        const cp = execFile(youtubedl, [...args, url], {
            cwd: cwd,
            maxBuffer: 1024 * 1024 * 10
        }, (err, out) => {
            cancel = () => {};
            if (err) {
                reject(err);
            } else {
                resolve(out);
            }
        }
        );
        cancel = ({ message }) => {
            terminate(cp.pid);
            reject(message);
        };
    });
}

/**
 * @function getInfo
 * @param  {Object} opts
 * @param  {String} opts.cwd
 * @param  {String} opts.url
 * @param  {String} opts.format
 */
function getInfo ({ cwd, url, format }) {
    let args = [];
    if (format) {
        args = [...args, '-f', format];
    }
    args = [...args, '--print-json', '-s'];
    return execute({ cwd, url, args });
}

/**
 * @function streamVideoIntoFile
 * @param  {Object} opts
 * @param  {String} opts.cwd
 * @param  {String} opts.url
 * @param  {String} opts.filename
 * @param  {String} opts.format
 */
function streamVideoIntoFile ({ cwd, url, filename, format }) {
    if (typeof filename !== 'string') {
        return Promise.reject('Filename required');
    }
    if (typeof url !== 'string') {
        return Promise.reject('url required');
    }
    if (typeof cwd !== 'string') {
        return Promise.reject('cwd required');
    }
    let args = [];
    if (format) {
        args = [...args, '-f', format];
    }
    args = [...args, '-o', filename, '--no-part'];
    return execute({cwd, url, args});
}

function abort () {
    cancel({
        message: 'Cancelled'
    });
}

export default {
    getInfo,
    abort,
    streamVideoIntoFile
};