import { execFile } from 'child_process';
import terminate from 'terminate';
import path from 'path';

const getFFMPEGPath = () => path.join(__statics, 'bin', 'ffmpeg.exe');

let cancel = () => Promise.resolve();

function execute ({ cwd, args }) {
  console.log({ cwd, args });
  return new Promise((resolve, reject) => {
    const cp = execFile(
      getFFMPEGPath(),
      [ ...args ],
      {
        cwd: cwd,
        shell: false,
        maxBuffer: 1024 * 1024 * 10
      },
      (err, out) => {
        cancel = () => Promise.resolve();
        console.log({ err, out });
        if (err) {
          reject(err);
        } else {
          resolve(out);
        }
      }
    );
    cancel = ({ message }) => {
      return new Promise((rResolve, rReject) => {
        terminate(cp.pid, rResolve);
        reject(message);
      });
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
  const [ input1, input2 ] = inputs;
  let args = [ '-i', input1, '-i', input2, '-shortest', `${output}` ];
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
  let convertArgs = [];
  switch (path.extname(output)) {
    case 'mp3':
      convertArgs = [ '-vn', '-ab', '256' ];
      break;
    case 'mp4':
      convertArgs = [ '-c:v', 'libx264' ];
      break;
    default:
      break;
  }
  let args = [ '-i', input, ...convertArgs, `${output}` ];
  return execute({ cwd, args });
}

function abort () {
  return cancel({
    message: 'Cancelled'
  });
}

export default {
  combine,
  convert,
  abort
};
