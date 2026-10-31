<?php
declare(strict_types=1);

/**
 * Router for PHP Built-in Web Server (تشغيل_لوحة_التحكم.bat)
 * Enforces the same security rules as .htaccess when running via php -S 127.0.0.1:8099.
 */

$uri = urldecode((string)parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH));

if (
    preg_match('#^/(includes|backups)(/|$)#i', $uri) ||
    preg_match('#/(config\.[^/]+|.*\.(json|bat|md|sql|gz)|\.htaccess)$#i', $uri)
) {
    http_response_code(403);
    header('Content-Type: text/plain; charset=utf-8');
    exit('403 Forbidden');
}

$fullPath = __DIR__ . $uri;
if ($uri !== '/' && is_file($fullPath) && !preg_match('/\.php$/i', $uri)) {
    return false; // Serve static CSS/JS/images directly
}

if ($uri === '/api.php') {
    require __DIR__ . '/api.php';
    return true;
}

require __DIR__ . '/index.php';
return true;
